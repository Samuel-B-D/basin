using System.Numerics;
using Basin.Audio.PipeWire;
using Xunit;

namespace DungeonWm.Tests;

public sealed class DoorwayTests
{
    private static readonly Lazy<DungeonWmWorld> SharedWorld = new(() =>
    {
        var world = new DungeonWmWorld(Kit.Load(), new DungeonWmSettings());
        for (var i = 0; i < 4; i++)
        {
            world.AddRoom();
        }

        return world;
    });

    private static (DungeonWmWorld World, DoorwayGraph Graph) Build()
    {
        var world = SharedWorld.Value;
        var graph = new DoorwayGraph();
        graph.Build(world.Map, world.Unit);
        return (world, graph);
    }

    [Fact]
    public void A_source_in_the_same_room_is_direct()
    {
        var (world, graph) = Build();
        var room = world.Map.Rooms[0];
        var listener = world.CenterOf(room, 1.6f);
        var source = world.EntranceOf(room, 1.6f);

        var path = graph.Path(listener, source);

        Assert.Equal(0, path.Doorways);
        Assert.Equal(Vector3.Distance(listener, source), path.Length, 3);
        Assert.Equal(source, path.Apparent);
    }

    [Fact]
    public void The_next_room_is_one_doorway_away_and_seems_to_come_from_the_nearest_doorway()
    {
        var (world, graph) = Build();
        var listener = world.CenterOf(world.Map.Rooms[1], 1.6f);
        var source = world.CenterOf(world.Map.Rooms[0], 1.6f);

        var path = graph.Path(listener, source);

        Assert.Equal(1, path.Doorways);
        Assert.True(path.Length > Vector3.Distance(listener, source) - 0.001f);
        var nearest = Enumerable.Range(0, graph.Count).Select(graph.CenterOf).MinBy(center => Vector3.Distance(center, listener));
        Assert.Equal(nearest, path.Apparent);
    }

    [Fact]
    public void Two_rooms_away_is_two_doorways_and_the_path_is_longer()
    {
        var (world, graph) = Build();
        var source = world.CenterOf(world.Map.Rooms[0], 1.6f);
        var next = graph.Path(world.CenterOf(world.Map.Rooms[1], 1.6f), source);
        var further = graph.Path(world.CenterOf(world.Map.Rooms[2], 1.6f), source);

        Assert.Equal(2, further.Doorways);
        Assert.True(further.Length > next.Length);
    }

    [Fact]
    public void The_first_doorway_sets_the_cutoff_and_each_later_one_adds_the_loss()
    {
        var settings = new AudioSettings();
        var one = new SoundPath(new Vector3(0, 0, -2), 2, 1, true);
        var two = one with { Doorways = 2 };
        var direct = one with { Doorways = 0 };

        var open = Acoustics.Place(Vector3.Zero, Quaternion.Identity, direct, settings, pan: false);
        var first = Acoustics.Place(Vector3.Zero, Quaternion.Identity, one, settings, pan: false);
        var second = Acoustics.Place(Vector3.Zero, Quaternion.Identity, two, settings, pan: false);

        Assert.Equal(VoiceParams.OpenCutoffHz, open.CutoffHz);
        Assert.Equal(settings.DoorwayCutoffHz, first.CutoffHz, 3);
        Assert.Equal(open.GainDb, first.GainDb, 3);
        Assert.Equal(settings.DoorwayCutoffHz / MathF.Pow(2, settings.DoorwayOctaves), second.CutoffHz, 3);
        Assert.Equal(first.GainDb - settings.DoorwayLossDb, second.GainDb, 3);
    }
}
