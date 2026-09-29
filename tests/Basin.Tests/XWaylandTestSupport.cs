using System.Diagnostics;
using Xunit;

namespace Basin.Tests;

internal static class XWaylandTestSupport
{
    public static void Pump(CompositorTestHost host, Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(15))
        {
            host.Loop.Dispatch(10);
            host.Display.FlushClients();
        }

        Assert.True(condition(), "condition not reached within 15 s");
    }

    public static Process Start(string program, string display, params string[] args)
    {
        var info = new ProcessStartInfo(program, args) { UseShellExecute = false, RedirectStandardError = true };
        info.Environment["DISPLAY"] = display;
        info.Environment.Remove("WAYLAND_DISPLAY");
        return Process.Start(info) ?? throw new InvalidOperationException($"{program} did not start");
    }

    public static void Run(CompositorTestHost host, string display, params string[] args)
    {
        using var process = Start("xprop", display, args);
        Pump(host, () => process.HasExited);
        Assert.Equal(0, process.ExitCode);
    }
}
