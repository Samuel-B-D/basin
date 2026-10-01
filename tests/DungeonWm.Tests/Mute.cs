using System.Runtime.CompilerServices;

namespace DungeonWm.Tests;

internal static class Mute
{
    [ModuleInitializer]
    internal static void Everything() => Environment.SetEnvironmentVariable("BASIN_MUTE", "1");
}
