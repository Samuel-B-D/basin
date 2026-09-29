using System.Numerics;
using Xunit;

namespace DungeonWm.Tests;

public sealed class MapTests
{
    private static readonly Lazy<Kit> SharedKit = new(Kit.Load);

    [Fact]
    public void Every_corner_prop_clears_both_walls_and_the_corner_pillar()
    {
        var world = new DungeonWmWorld(SharedKit.Value, new DungeonWmSettings());
        for (var i = 0; i < 6; i++)
        {
            world.AddRoom();
        }

        var clear = RoomBaker.PillarHalf;
        foreach (var region in world.Map.Rooms)
        {
            var corners = new (int X, int Z, float Yaw)[]
            {
                (region.X0, region.Z0, MathF.PI * 0.25f),
                (region.X0 + region.Width - 1, region.Z0, -MathF.PI * 0.25f),
                (region.X0, region.Z0 + region.Depth - 1, MathF.PI * 0.75f),
                (region.X0 + region.Width - 1, region.Z0 + region.Depth - 1, -MathF.PI * 0.75f),
            };
            foreach (var (x, z, yaw) in corners)
            {
                foreach (var piece in RoomBaker.StaticProps.Concat(RoomBaker.DynamicPieces))
                {
                    var at = world.Baker.CornerPosition(region, x, z, yaw, piece);
                    var bounds = SharedKit.Value[piece].Bounds.Transformed(Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(at));
                    var where = $"{piece} in room {region.Workspace + 1} at {x},{z}";
                    Assert.True(bounds.Min.X >= (4 * region.X0) + clear, where);
                    Assert.True(bounds.Max.X <= (4 * (region.X0 + region.Width)) - clear, where);
                    Assert.True(bounds.Min.Z >= (4 * region.Z0) + clear, where);
                    Assert.True(bounds.Max.Z <= (4 * (region.Z0 + region.Depth)) - clear, where);
                }
            }
        }
    }

    [Fact]
    public void A_chain_of_nine_rooms_never_overlaps_and_touches_only_at_doorways()
    {
        var map = Chain(9);
        Assert.Equal(9, map.Rooms.Count);
        Assert.Equal(8, map.Regions.Count(r => r.Kind == RegionKind.Corridor));

        var seen = new Dictionary<GridCell, MapRegion>();
        foreach (var region in map.Regions)
        {
            foreach (var cell in region.Cells)
            {
                Assert.True(seen.TryAdd(cell, region), $"cell {cell} is in two regions");
            }
        }

        foreach (var region in map.Regions)
        {
            foreach (var cell in region.Cells)
            {
                foreach (var side in GridSides.All)
                {
                    if (!seen.TryGetValue(cell.Step(side), out var other) || ReferenceEquals(other, region))
                    {
                        continue;
                    }

                    var edge = new MapEdge(cell, side).Canonical;
                    Assert.Contains(map.Doorways, d => d.Edge == edge);
                }
            }
        }

        Assert.Equal(16, map.Doorways.Count);
    }

    [Fact]
    public void The_same_seed_builds_the_same_chain()
    {
        var a = Chain(6);
        var b = Chain(6);
        Assert.Equal(
            a.Regions.SelectMany(r => r.Cells).ToArray(),
            b.Regions.SelectMany(r => r.Cells).ToArray());
    }

    [Fact]
    public void Every_room_has_slots_a_torch_and_a_doorway()
    {
        var map = Chain(9);
        foreach (var room in map.Rooms)
        {
            var walls = map.WallsOf(room);
            Assert.Contains(walls, w => w.Kind == WallKind.Slot);
            Assert.Contains(walls, w => w.Torch);
            Assert.Contains(walls, w => w.Kind == WallKind.Doorway);
        }
    }

    [Fact]
    public void The_baked_roof_faces_down_and_covers_every_floor_cell()
    {
        var map = Chain(9);
        var baker = new RoomBaker(SharedKit.Value, new BakeSettings());
        foreach (var region in map.Regions)
        {
            var baked = baker.Bake(map, region);
            var roofTop = RoomBaker.WallHeight * baker.Unit;
            var covered = new HashSet<GridCell>();
            var mesh = baked.Mesh;
            for (var i = 0; i < mesh.Indices.Length; i += 3)
            {
                var a = mesh.Vertices[mesh.Indices[i]].Position;
                var b = mesh.Vertices[mesh.Indices[i + 1]].Position;
                var c = mesh.Vertices[mesh.Indices[i + 2]].Position;
                if (MathF.Abs(a.Y - roofTop) > 0.01f || MathF.Abs(b.Y - roofTop) > 0.01f || MathF.Abs(c.Y - roofTop) > 0.01f)
                {
                    continue;
                }

                var normal = Vector3.Cross(b - a, c - a);
                if (normal.LengthSquared() < 1e-12f)
                {
                    continue;
                }

                var center = (a + b + c) / 3 / baker.Unit;
                var inCellX = center.X - (4 * MathF.Floor(center.X / 4));
                var inCellZ = center.Z - (4 * MathF.Floor(center.Z / 4));
                if (inCellX < 0.6f || inCellX > 3.4f || inCellZ < 0.6f || inCellZ > 3.4f)
                {
                    continue;
                }

                Assert.True(Vector3.Normalize(normal).Y < -0.99f, "a roof triangle faces up");
                covered.Add(new GridCell((int)MathF.Floor(center.X / 4), (int)MathF.Floor(center.Z / 4)));
            }

            foreach (var cell in region.Cells)
            {
                Assert.Contains(cell, covered);
            }
        }
    }

    [Fact]
    public void A_fixed_seed_bakes_the_first_room_to_a_known_size()
    {
        var map = Chain(1);
        var baked = new RoomBaker(SharedKit.Value, new BakeSettings()).Bake(map, map.Rooms[0]);
        var room = map.Rooms[0];
        Assert.Equal((3, 5), (room.Width, room.Depth));
        Assert.Equal(ExpectedTriangles, baked.Mesh.TriangleCount);
    }

    [Fact]
    public void A_full_room_grows_away_from_its_entrance_and_keeps_its_cells()
    {
        var map = Chain(3);
        var room = map.Rooms[2];
        var before = room.Cells.ToHashSet();
        var slots = map.WallsOf(room).Count(w => w.Kind == WallKind.Slot);
        Assert.True(map.Grow(room));
        Assert.True(before.IsSubsetOf(room.Cells.ToHashSet()));
        Assert.True(map.WallsOf(room).Count(w => w.Kind == WallKind.Slot) > slots);
    }

    private const int ExpectedTriangles = 15791;

    private static DungeonWmMap Chain(int rooms)
    {
        var map = new DungeonWmMap(new MapSettings());
        for (var i = 0; i < rooms; i++)
        {
            map.AddRoom();
        }

        return map;
    }
}
