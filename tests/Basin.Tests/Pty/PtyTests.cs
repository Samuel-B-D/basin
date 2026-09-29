using System.Diagnostics;
using System.Text;
using Basin.Pty;
using Xunit;

namespace Basin.Tests.Pty;

public sealed class PtyTests
{
    [Fact]
    public void The_child_runs_on_a_terminal_of_the_size_set_before_the_spawn()
    {
        using var host = new CompositorTestHost();
        var run = Run.Start(host, ["sh", "-c", "tty; stty size"], columns: 100, rows: 30);
        run.WaitForExit();
        run.Session.Dispose();

        Assert.Contains("/dev/pts/", run.Text);
        Assert.Contains("30 100", run.Text);
        Assert.Equal(new PtyExit(0, null), run.Exit);
    }

    [Fact]
    public void A_resize_reaches_the_child()
    {
        using var host = new CompositorTestHost();
        var run = Run.Start(host, ["sh", "-c", "stty size; read line; stty size"]);
        run.WaitFor(() => run.Text.Contains("24 80"));
        run.Session.Resize(132, 43, 0, 0);
        run.Session.Write("\r"u8);
        run.WaitForExit();
        run.Session.Dispose();

        Assert.Contains("43 132", run.Text);
    }

    [Fact]
    public void The_exit_status_is_reported_once_after_the_last_output()
    {
        using var host = new CompositorTestHost();
        var run = Run.Start(host, ["sh", "-c", "printf last; exit 3"]);
        run.WaitForExit();
        run.Dispatch(50);
        run.Session.Dispose();

        Assert.Equal(1, run.ExitCount);
        Assert.Equal(new PtyExit(3, null), run.Exit);
        Assert.EndsWith("last", run.TextAtExit);
    }

    [Fact]
    public void A_signal_is_reported_as_the_signal()
    {
        using var host = new CompositorTestHost();
        var run = Run.Start(host, ["sh", "-c", "kill -TERM $$"]);
        run.WaitForExit();
        run.Session.Dispose();

        Assert.Equal(new PtyExit(null, 15), run.Exit);
    }

    [Fact]
    public void The_end_of_file_after_the_child_closes_the_terminal_loses_no_output()
    {
        using var host = new CompositorTestHost();
        var run = Run.Start(host, ["sh", "-c", "head -c 204800 /dev/zero"]);
        run.WaitForExit();
        run.Session.Dispose();

        Assert.Equal(204800, run.BytesAtExit);
        Assert.Equal(new PtyExit(0, null), run.Exit);
    }

    [Fact]
    public void A_burst_is_read_across_several_dispatches_each_within_the_budget()
    {
        using var host = new CompositorTestHost();
        var run = Run.Start(host, ["sh", "-c", "head -c 1048576 /dev/zero"]);
        var perDispatch = new List<int>();
        var watch = Stopwatch.StartNew();
        while (run.ExitCount == 0 && watch.ElapsedMilliseconds < 10000)
        {
            var before = run.Bytes;
            host.Loop.Dispatch(100);
            if (run.Bytes > before)
            {
                perDispatch.Add(run.Bytes - before);
            }
        }

        run.Session.Dispose();

        Assert.Equal(1048576, run.Bytes);
        Assert.True(perDispatch.Count > 1, $"one dispatch read {perDispatch.Count} chunks");
        Assert.All(perDispatch, count => Assert.InRange(count, 1, PtySession.ReadBudget));
    }

    [Fact]
    public void A_large_write_is_queued_rather_than_blocking()
    {
        using var host = new CompositorTestHost();
        var run = Run.Start(host, ["sh", "-c", "stty -echo; echo ready; head -c 1048576 > /dev/null; printf done"]);
        run.WaitFor(() => run.Text.Contains("ready"));
        var line = Encoding.ASCII.GetBytes(new string('a', 1023) + "\n");
        var data = new byte[1048576];
        for (var i = 0; i < data.Length; i += line.Length)
        {
            line.CopyTo(data.AsSpan(i));
        }

        var watch = Stopwatch.StartNew();
        run.Session.Write(data);
        var elapsed = watch.ElapsedMilliseconds;
        Assert.True(run.Session.QueuedBytes > 0);
        Assert.True(elapsed < 500, $"the write took {elapsed} ms");

        run.WaitForExit();
        run.Session.Dispose();

        Assert.EndsWith("done", run.TextAtExit);
        Assert.Equal(new PtyExit(0, null), run.Exit);
    }

    [Fact]
    public void Dispose_hangs_up_the_childs_process_group()
    {
        using var host = new CompositorTestHost();
        var marker = Path.Combine(Path.GetTempPath(), $"basin-pty-{Environment.ProcessId}-{Guid.NewGuid():N}");
        try
        {
            var run = Run.Start(host, ["sh", "-c", $"trap 'echo hup > {marker}; exit 0' HUP; echo ready; while :; do sleep 0.05; done"]);
            run.WaitFor(() => run.Text.Contains("ready"));
            run.Session.Dispose();
            run.WaitFor(() => PtyReaper.Pending == 0);

            Assert.Equal(0, run.ExitCount);
            Assert.Equal("hup", File.ReadAllText(marker).Trim());
        }
        finally
        {
            File.Delete(marker);
        }
    }

    [Fact]
    public void Collecting_orphans_kills_a_child_that_ignores_the_hangup()
    {
        using var host = new CompositorTestHost();
        var run = Run.Start(host, ["sh", "-c", "trap '' HUP; echo ready; while :; do sleep 0.05; done"]);
        run.WaitFor(() => run.Text.Contains("ready"));
        run.Session.Dispose();
        Assert.Equal(1, PtySession.Orphans);

        PtySession.CollectOrphans(TimeSpan.FromMilliseconds(100));

        Assert.Equal(0, PtySession.Orphans);
    }

    [Fact]
    public void A_program_that_does_not_exist_fails_to_start_by_name()
    {
        using var host = new CompositorTestHost();
        var session = PtySession.TryStart(host.Loop, new PtyStartInfo(["basin-no-such-program"]), out var failure);

        Assert.Null(session);
        Assert.Contains("basin-no-such-program", failure);
    }

    private sealed class Run
    {
        private readonly CompositorTestHost _host;
        private readonly List<byte> _output = [];

        private Run(CompositorTestHost host, PtySession session)
        {
            _host = host;
            Session = session;
            session.Output += data =>
            {
                foreach (var b in data)
                {
                    _output.Add(b);
                }
            };
            session.Exited += exit =>
            {
                Exit = exit;
                ExitCount++;
                BytesAtExit = _output.Count;
                TextAtExit = Text;
            };
        }

        public PtySession Session { get; }

        public PtyExit? Exit { get; private set; }

        public int ExitCount { get; private set; }

        public int Bytes => _output.Count;

        public int BytesAtExit { get; private set; }

        public string TextAtExit { get; private set; } = "";

        public string Text => Encoding.UTF8.GetString([.. _output]);

        public static Run Start(CompositorTestHost host, string[] argv, int columns = 80, int rows = 24)
        {
            var session = PtySession.TryStart(host.Loop, new PtyStartInfo(argv) { Columns = columns, Rows = rows }, out var failure);
            Assert.True(session is not null, failure);
            return new Run(host, session!);
        }

        public void WaitForExit() => WaitFor(() => ExitCount > 0);

        public void WaitFor(Func<bool> condition)
        {
            var watch = Stopwatch.StartNew();
            while (!condition())
            {
                Assert.True(watch.ElapsedMilliseconds < 10000, $"timed out; output so far: {Text}");
                _host.Loop.Dispatch(50);
            }
        }

        public void Dispatch(int milliseconds)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds)
            {
                _host.Loop.Dispatch(10);
            }
        }
    }
}
