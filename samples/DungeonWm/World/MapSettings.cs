namespace DungeonWm;

internal sealed record MapSettings(
    int Seed = 7,
    int RoomMinWidth = 3,
    int RoomMinDepth = 3,
    int RoomMaxWidth = 6,
    int RoomMaxDepth = 5,
    double DecorWalls = 0.2,
    int TorchSpacing = 2);
