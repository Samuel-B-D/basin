using System.Runtime.CompilerServices;

namespace System7Wm.Tests;

internal static class Mute
{
    [ModuleInitializer]
    internal static void Everything()
    {
        Environment.SetEnvironmentVariable("BASIN_MUTE", "1");
        Environment.SetEnvironmentVariable("PIPEWIRE_REMOTE", "/nonexistent/system7-wm-tests/pipewire-0");
    }
}
