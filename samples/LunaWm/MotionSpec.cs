namespace LunaWm;

internal readonly record struct MotionSpec(string Key, int DurationMs, QtCurve Curve, int PauseMs = 0);
