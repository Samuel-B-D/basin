using Basin.Diagnostics;
using Xunit;

namespace Basin.Tests;

public sealed class ProtocolTraceTests
{
    [Fact]
    public void A_traced_frame_done_does_not_trip_the_allocation_guard_and_the_guard_returns()
    {
        using var host = new CompositorTestHost();
        var window = MappedToplevel.Map(host, host.Client);
        var previous = (BasinLog.Level, BasinLog.Sink);
        var sink = new CountingSink();
        BasinLog.Level = BasinLogLevel.Debug;
        BasinLog.Sink = sink;
        try
        {
            using (WaylandDiagnostics.TraceProtocol(host.Display))
            {
                Assert.False(AllocationScope.Enabled);
                for (var i = 0; i < 4; i++)
                {
                    window.Surface.Frame();
                    window.Surface.Commit();
                    host.PumpToServer();
                    host.Scene.SendFrameDone(1);
                    host.PumpToClient();
                }
            }

            Assert.True(AllocationScope.Enabled);
            Assert.True(sink.Count > 0, "the trace logged the frame traffic");
        }
        finally
        {
            (BasinLog.Level, BasinLog.Sink) = previous;
        }
    }

    private sealed class CountingSink : IBasinLogSink
    {
        public int Count;

        public void Write(BasinLogLevel level, string category, ReadOnlySpan<char> message) => Count++;
    }
}
