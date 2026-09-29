using System.Diagnostics;
using Xunit;

namespace System7Wm.Tests;

internal sealed class Client : IDisposable
{
    private readonly Process _process;
    private readonly List<string> _lines = [];
    private readonly Lock _gate = new();

    public Client(Process process)
    {
        _process = process;
        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (_gate)
                {
                    _lines.Add(e.Data);
                }
            }
        };
        _process.ErrorDataReceived += (_, _) => { };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public int Mark()
    {
        lock (_gate)
        {
            return _lines.Count;
        }
    }

    public int Count(Func<string, bool> predicate, int since = 0)
    {
        lock (_gate)
        {
            return _lines.Skip(since).Count(predicate);
        }
    }

    public async Task<string?> WaitForAsync(Func<string, bool> predicate, int since = 0, int attempts = 100)
    {
        for (var i = 0; i < attempts; i++)
        {
            lock (_gate)
            {
                for (var j = _lines.Count - 1; j >= since; j--)
                {
                    if (predicate(_lines[j]))
                    {
                        return _lines[j];
                    }
                }
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        return null;
    }

    public Task<string?> WaitForLineAsync(string exact, int since = 0) => WaitForAsync(line => line == exact, since);

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }

            _process.WaitForExit(3000);
        }
        catch (InvalidOperationException)
        {
        }

        _process.Dispose();
    }
}
