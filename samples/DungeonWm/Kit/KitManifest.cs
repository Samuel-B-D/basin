namespace DungeonWm;

internal static class KitManifest
{
    public const string Avatar = "KayKit/Characters/Rogue_Hooded.glb";

    public static readonly KitPiece[] Pieces =
    [
        new("wall", KitRole.SlotWall, KitShape.Box),
        new("wall_cracked", KitRole.DecorWall, KitShape.Box),
        new("wall_window_open", KitRole.DecorWall, KitShape.Box),
        new("wall_window_closed", KitRole.DecorWall, KitShape.Box),
        new("wall_shelves", KitRole.DecorWall, KitShape.Box),
        new("wall_arched", KitRole.Arch, KitShape.Box),
        new("wall_doorway", KitRole.Doorway, KitShape.None, "wall_doorway_door"),
        new("wall_corner", KitRole.Corner, KitShape.Box),
        new("wall_Tsplit", KitRole.TSplit, KitShape.Box),
        new("wall_crossing", KitRole.Crossing, KitShape.Box),
        new("floor_tile_large", KitRole.Floor, KitShape.Box),
        new("floor_tile_small", KitRole.Floor, KitShape.Box),
        new("floor_wood_large", KitRole.Floor, KitShape.Box),
        new("pillar", KitRole.Pillar, KitShape.Box),
        new("pillar_decorated", KitRole.Pillar, KitShape.Box),
        new("torch_mounted", KitRole.Torch, KitShape.None),
        new("table_medium", KitRole.StaticProp, KitShape.Box),
        new("table_long", KitRole.StaticProp, KitShape.Box),
        new("table_small", KitRole.StaticProp, KitShape.Box),
        new("shelf_small", KitRole.StaticProp, KitShape.None),
        new("shelves", KitRole.StaticProp, KitShape.Box),
        new("banner_red", KitRole.StaticProp, KitShape.None),
        new("banner_blue", KitRole.StaticProp, KitShape.None),
        new("banner_patternA_green", KitRole.StaticProp, KitShape.None),
        new("chest", KitRole.StaticProp, KitShape.Box),
        new("keg", KitRole.StaticProp, KitShape.Box),
        new("candle_lit", KitRole.StaticProp, KitShape.None),
        new("candle_triple", KitRole.StaticProp, KitShape.None),
        new("rubble_large", KitRole.StaticProp, KitShape.Box),
        new("rubble_half", KitRole.StaticProp, KitShape.Box),
        new("barrel_small", KitRole.DynamicProp, KitShape.Cylinder),
        new("barrel_large", KitRole.DynamicProp, KitShape.Cylinder),
        new("box_small", KitRole.DynamicProp, KitShape.Box),
        new("box_large", KitRole.DynamicProp, KitShape.Box),
        new("crates_stacked", KitRole.DynamicProp, KitShape.Box),
        new("chair", KitRole.DynamicProp, KitShape.Box),
        new("stool", KitRole.DynamicProp, KitShape.Cylinder),
    ];

    public static KitPiece Find(string name)
    {
        foreach (var piece in Pieces)
        {
            if (piece.Name == name)
            {
                return piece;
            }
        }

        throw new KeyNotFoundException($"The kit has no piece named {name}.");
    }
}
