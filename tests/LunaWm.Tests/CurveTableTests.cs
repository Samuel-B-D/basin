using Xunit;

namespace LunaWm.Tests;

public sealed class CurveTableTests
{
    private const string LunaAnimationsConf = """
        [Cards]
        cardLaunchDuration=400
        cardLaunchCurve=40
        cardSlideDuration=300
        cardSlideCurve=10
        cardMaximizeDuration=300
        cardMaximizeCurve=10
        cardMinimizeDuration=350
        cardMinimizeCurve=6
        cardDeleteDuration=300
        cardDeleteCurve=6
        cardScootAwayOnLaunchDuration=400
        cardScootAwayOnLaunchCurve=30
        cardShuffleReorderDuration=350
        cardShuffleReorderCurve=6
        cardGroupReorderDuration=500
        cardGroupReorderCurve=6
        cardSwitchReachEndMaximizedDuration=200
        cardSwitchReachEndMaximizedCurve=6
        cardSwitchMaximizedDuration=350
        cardSwitchMaximizedCurve=6
        cardLoadingPulsePauseDuration=1000
        cardLoadingPulseDuration=1000
        cardLoadingPulseCurve=1
        cardGhostDuration=750
        cardGhostCurve=10
        cardDimmingDuration=300
        cardDimmingCurve=6

        [Dashboard]
        dashboardSnapDuration=500
        dashboardSnapCurve=6

        [Launcher]
        quickLaunchDuration=350
        quickLaunchCurve=6
        launcherDuration=350
        launcherCurve=15
        universalSearchCrossFadeDuration=150
        universalSearchCrossFadeCurve=6

        [Lock]
        lockWindowFadeDuration=150
        lockWindowFadeCurve=1
        lockPinDuration=250
        lockPinCurve=15
        lockFadeDuration=200
        lockFadeCurve=0

        [StatusBar]
        statusBarFadeDuration=300
        statusBarFadeCurve=0
        statusBarTitleChangeDuration=300
        statusBarTitleChangeCurve=0
        statusBarMenuFadeDuration=200
        statusBarMenuFadeCurve=0

        [Spaces]
        positiveSpaceChangeDuration=400
        positiveSpaceChangeCurve=6
        """;

    private static readonly string[] Qt48Order =
    [
        "Linear",
        "InQuad", "OutQuad", "InOutQuad", "OutInQuad",
        "InCubic", "OutCubic", "InOutCubic", "OutInCubic",
        "InQuart", "OutQuart", "InOutQuart", "OutInQuart",
        "InQuint", "OutQuint", "InOutQuint", "OutInQuint",
        "InSine", "OutSine", "InOutSine", "OutInSine",
        "InExpo", "OutExpo", "InOutExpo", "OutInExpo",
        "InCirc", "OutCirc", "InOutCirc", "OutInCirc",
        "InElastic", "OutElastic", "InOutElastic", "OutInElastic",
        "InBack", "OutBack", "InOutBack", "OutInBack",
        "InBounce", "OutBounce", "InOutBounce", "OutInBounce",
    ];

    private static Dictionary<string, int> Conf()
    {
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in LunaAnimationsConf.Split('\n'))
        {
            var parts = line.Trim().Split('=');
            if (parts.Length == 2)
            {
                values[parts[0]] = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return values;
    }

    [Fact]
    public void The_curve_names_follow_qt_4_8_enum_order()
    {
        for (var i = 0; i < Qt48Order.Length; i++)
        {
            Assert.Equal(Qt48Order[i], ((QtCurve)i).ToString());
        }
    }

    [Fact]
    public void Every_motion_matches_luna_animations_conf()
    {
        var conf = Conf();
        foreach (var spec in LunaMotion.All)
        {
            Assert.Equal(conf[$"{spec.Key}Duration"], spec.DurationMs);
            Assert.Equal(conf[$"{spec.Key}Curve"], (int)spec.Curve);
            if (spec.PauseMs > 0)
            {
                Assert.Equal(conf[$"{spec.Key}PauseDuration"], spec.PauseMs);
            }
        }
    }

    [Theory]
    [InlineData((int)QtCurve.Linear, 0.25, 0.25)]
    [InlineData((int)QtCurve.InQuad, 0.5, 0.25)]
    [InlineData((int)QtCurve.OutQuad, 0.5, 0.75)]
    [InlineData((int)QtCurve.OutCubic, 0.5, 0.875)]
    [InlineData((int)QtCurve.OutQuart, 0.5, 0.9375)]
    [InlineData((int)QtCurve.InOutQuint, 0.25, 0.015625)]
    [InlineData((int)QtCurve.InOutQuint, 0.75, 0.984375)]
    [InlineData((int)QtCurve.OutBounce, 0.5, 0.765625)]
    public void Curves_match_penner_values(int curve, double t, double expected) =>
        Assert.Equal(expected, QtEasing.Evaluate((QtCurve)curve, t), 6);

    [Theory]
    [InlineData((int)QtCurve.OutElastic)]
    [InlineData((int)QtCurve.OutInBounce)]
    [InlineData((int)QtCurve.InOutBack)]
    [InlineData((int)QtCurve.OutExpo)]
    public void Every_curve_starts_at_zero_and_ends_at_one(int curve)
    {
        Assert.Equal(0, QtEasing.Evaluate((QtCurve)curve, 0), 3);
        Assert.Equal(1, QtEasing.Evaluate((QtCurve)curve, 1), 3);
    }

    [Fact]
    public void Out_elastic_overshoots_and_out_in_bounce_touches_the_middle()
    {
        var peak = 0.0;
        for (var t = 0.0; t <= 1; t += 0.01)
        {
            peak = Math.Max(peak, QtEasing.Evaluate(QtCurve.OutElastic, t));
        }

        Assert.InRange(peak, 1.2, 1.5);
        Assert.Equal(0.5, QtEasing.Evaluate(QtCurve.OutInBounce, 0.5), 3);
    }
}
