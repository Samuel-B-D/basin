using System.Runtime.CompilerServices;

namespace LunaWm.Tests;

internal static class Mute
{
    [ModuleInitializer]
    internal static void Everything()
    {
        Environment.SetEnvironmentVariable("BASIN_MUTE", "1");
        Environment.SetEnvironmentVariable("PIPEWIRE_REMOTE", "/nonexistent/luna-wm-tests/pipewire-0");
    }
}
