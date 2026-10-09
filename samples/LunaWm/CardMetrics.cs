namespace LunaWm;

internal readonly record struct CardMetrics
{
    public const double OriginRatio = 0.40;
    public const double MinimumScale = 0.26;
    public const int SearchPill = 48;
    public const int ClosedSpacing = 7;
    public const int MaxClosedSpacedCards = 3;
    public const int MaxStationaryCards = 4;
    public const double VelocityPerPosition = 1000;
    public const double TouchPadWidth = 1024;

    public CardMetrics(int width, int height, int top, double activeRatio, double inactiveRatio, double rotationFactor, int gap, double dim, int keyboardInset = 0)
    {
        Width = width;
        Height = height;
        Top = top;
        var normal = Math.Max(1, height - top);
        CardWidth = width;
        CardHeight = normal;
        ActiveScale = Math.Max(MinimumScale, (normal - SearchPill) * activeRatio / normal);
        InactiveScale = Math.Max(MinimumScale, (normal - SearchPill) * inactiveRatio / normal);
        Origin = top + SearchPill + ((normal - SearchPill) * OriginRatio);
        RotationFactor = rotationFactor;
        Gap = gap;
        Dim = dim;
        KeyboardInset = keyboardInset;
    }

    public int KeyboardInset { get; }

    public int Width { get; }

    public int Height { get; }

    public int Top { get; }

    public double CardWidth { get; }

    public double CardHeight { get; }

    public double ActiveScale { get; }

    public double InactiveScale { get; }

    public double Origin { get; }

    public double RotationFactor { get; }

    public int Gap { get; }

    public double Dim { get; }

    public double ActiveCardWidth => CardWidth * ActiveScale;

    public double PositionsPerPixel => 3.0 / ActiveCardWidth;

    public CardPose Maximized => new(Width / 2.0, Top + ((CardHeight - KeyboardInset) / 2.0), 1, 0, 1);

    public CardPose Fullscreen => new(Width / 2.0, Height / 2.0, 1, 0, 1);

    public double HalfExtent(in CardPose pose)
    {
        var radians = pose.Rotation * Math.PI / 180;
        var halfWidth = pose.Scale * CardWidth / 2;
        var halfHeight = pose.Scale * CardHeight / 2;
        return (halfWidth * Math.Abs(Math.Cos(radians))) + (halfHeight * Math.Abs(Math.Sin(radians)));
    }
}
