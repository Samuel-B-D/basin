using Basin;
using Basin.Effects;

namespace TinyComp;

internal sealed class GridRevealState
{
    public const double Feather = 24.0;

    private long _lastNanos;
    private bool _anchorPending;

    public double Raw { get; private set; }

    public int Direction { get; private set; }

    public double LingerMillis { get; private set; } = double.NaN;

    public double OriginX { get; private set; }

    public double OriginY { get; private set; }

    public bool Lingering => !double.IsNaN(LingerMillis);

    public bool Running => Direction != 0 || Lingering;

    public bool Shown => Raw > 0 || Running;

    public void Show(double x, double y, CanvasSetting settings)
    {
        if (Raw <= 0)
        {
            OriginX = x;
            OriginY = y;
        }

        LingerMillis = double.NaN;
        Direction = 1;
        Anchor();
        if (Jumps(settings))
        {
            Raw = 1;
            Direction = 0;
        }
    }

    public void Drop(CanvasSetting settings)
    {
        LingerMillis = settings.GridLingerMillis;
        Anchor();
        if (LingerMillis <= 0)
        {
            Hide(settings);
        }
    }

    public void Hide(CanvasSetting settings)
    {
        LingerMillis = double.NaN;
        Direction = Raw > 0 ? -1 : 0;
        if (Jumps(settings))
        {
            Raw = 0;
            Direction = 0;
        }
    }

    public void Reset()
    {
        Raw = 0;
        Direction = 0;
        LingerMillis = double.NaN;
    }

    public bool Step(in FrameTick tick, CanvasSetting settings)
    {
        if (!Running)
        {
            return false;
        }

        var nanos = tick.TargetPresentNanos;
        var delta = _anchorPending ? Math.Max(0, tick.RefreshIntervalNanos) : nanos - _lastNanos;
        _anchorPending = false;
        _lastNanos = nanos;
        var millis = Math.Clamp(delta / 1_000_000.0, 0.0, 250.0);
        if (Lingering)
        {
            LingerMillis -= millis;
            if (LingerMillis <= 0)
            {
                Hide(settings);
            }
        }

        if (Direction != 0)
        {
            var duration = settings.GridAnimationMillis;
            Raw = Jumps(settings) ? (Direction > 0 ? 1 : 0) : Math.Clamp(Raw + (Direction * millis / duration), 0.0, 1.0);
            if ((Direction > 0 && Raw >= 1) || (Direction < 0 && Raw <= 0))
            {
                Direction = 0;
            }
        }

        return Running;
    }

    public GridReveal RevealFor(CanvasSetting settings)
    {
        if (Raw >= 1)
        {
            return GridReveal.Full;
        }

        return new GridReveal(
            settings.GridRevealStyle, (float)settings.GridEasingValue.Apply(Raw), OriginX, OriginY, Feather);
    }

    private void Anchor()
    {
        _anchorPending = true;
    }

    private static bool Jumps(CanvasSetting settings) =>
        settings.GridAnimationValue == GridAnimationStyle.None || settings.GridAnimationMillis <= 0;
}
