namespace Basin.Diagnostics;

public static class BasinDiagnostics
{
    public const string TraceVariable = "BASIN_TRACE";

    private static readonly bool TraceRequested = Environment.GetEnvironmentVariable(TraceVariable) is not null;

    public static bool TraceEnabled => TraceRequested || BasinLog.Level == BasinLogLevel.Trace;

    public static void RouteToStandardError(BasinLogLevel? level = null)
    {
        BasinLog.Level = level ?? (TraceEnabled ? BasinLogLevel.Debug : BasinLogLevel.Warn);
        BasinLog.Sink = new StandardErrorLogSink();
        WaylandDiagnostics.RouteToBasinLog();
    }
}
