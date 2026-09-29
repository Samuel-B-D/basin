using System.Diagnostics;
using Xunit;

namespace Basin.Tests;

internal sealed class GMenuExporter : IDisposable
{
    private readonly Process _process;
    private readonly List<string> _activated = [];

    private readonly TaskCompletionSource<string> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private GMenuExporter(Process process)
    {
        _process = process;
        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not { } line)
            {
                return;
            }

            if (line.StartsWith("READY ", StringComparison.Ordinal))
            {
                _ready.TrySetResult(line["READY ".Length..]);
            }
            else if (line.StartsWith("ACTIVATED ", StringComparison.Ordinal))
            {
                lock (_activated)
                {
                    _activated.Add(line["ACTIVATED ".Length..]);
                }
            }
        };
        _process.BeginOutputReadLine();
    }

    public string UniqueName { get; private set; } = "";

    public IReadOnlyList<string> Activated
    {
        get
        {
            lock (_activated)
            {
                return [.. _activated];
            }
        }
    }

    public static GMenuExporter Start(PrivateBus bus)
    {
        var script = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gmenu-export.py");
        var probe = Process.Start(new ProcessStartInfo("python3", ["-c", "import gi; gi.require_version('Gio', '2.0'); from gi.repository import Gio"])
        {
            RedirectStandardError = true,
            UseShellExecute = false,
        });
        probe!.WaitForExit();
        Assert.SkipUnless(probe.ExitCode == 0, "python3 with PyGObject is not installed");
        var info = new ProcessStartInfo("python3", [script, bus.Address])
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        var process = Process.Start(info) ?? throw new InvalidOperationException("python3 did not start");
        var exporter = new GMenuExporter(process);
        Assert.True(exporter._ready.Task.Wait(10_000), "the GMenu exporter printed no READY line");
        exporter.UniqueName = exporter._ready.Task.Result;
        return exporter;
    }

    public void Dispose()
    {
        _process.StandardInput.Close();
        if (!_process.WaitForExit(3000))
        {
            _process.Kill();
        }

        _process.Dispose();
    }
}
