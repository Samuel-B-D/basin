namespace Basin.Effects;

public static class VistaDwmKernel
{
    public const int ExpandSize = 8;

    public const float TapSpacing = 1.216f;

    public static ReadOnlySpan<float> Offsets => [-1.824f, -0.608f, 0.608f, 1.824f];

    public static ReadOnlySpan<float> Weights =>
    [
        0.02800f, 0.04784f, 0.05251f, 0.03851f,
        0.04784f, 0.08168f, 0.09191f, 0.06534f,
        0.05251f, 0.09101f, 0.10268f, 0.07235f,
        0.03851f, 0.06534f, 0.07235f, 0.05251f,
    ];
}
