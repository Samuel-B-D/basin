using Wayland;
using Wayland.Server;

namespace Basin.Diagnostics;

public static class WaylandDiagnostics
{
    private static readonly BasinLogger Log = BasinLog.For("wayland");

    private static bool _routed;

    public static void RouteToBasinLog()
    {
        if (_routed || !WaylandLog.IsSupported)
        {
            return;
        }

        _routed = true;
        try
        {
            WaylandLog.SetHandler(WaylandLogSide.Server, static message => Log.Warn($"libwayland: {message}"));
            WaylandLog.SetHandler(WaylandLogSide.Client, static message => Log.Warn($"libwayland-client: {message}"));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    public static IDisposable TraceProtocol(WlServerDisplay display)
    {
        ArgumentNullException.ThrowIfNull(display);
        var logger = display.AddProtocolLogger(static (in WlProtocolMessage message) =>
        {
            if (BasinLog.IsEnabled(BasinLogLevel.Debug))
            {
                var line = message.ToString();
                Log.Debug($"{line}");
            }
        });
        return new ProtocolTrace(logger);
    }

    private sealed class ProtocolTrace : IDisposable
    {
        private readonly IDisposable _logger;
        private readonly bool _checked;
        private bool _disposed;

        public ProtocolTrace(IDisposable logger)
        {
            _logger = logger;
            _checked = AllocationScope.Enabled;
            AllocationScope.Enabled = false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _logger.Dispose();
            AllocationScope.Enabled = _checked;
        }
    }
}
