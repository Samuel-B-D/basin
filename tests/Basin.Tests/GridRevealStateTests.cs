using Basin.Effects;
using Xunit;

namespace Basin.Tests;

public sealed class GridRevealStateTests
{
    private static readonly FrameTick First = new(1_000_000_000, 16_666_667);

    private static TinyComp.CanvasSetting Settings(TinyComp.GridAnimationStyle style) => new TinyComp.CanvasSetting
    {
        GridAnimation = style,
        GridAnimationMs = 400,
        GridEasing = EasingCurve.Linear,
        GridLingerMs = 0,
    }.Over(TinyComp.CanvasSetting.Defaults);

    private static FrameTick After(in FrameTick tick, int millis) =>
        tick with { TargetPresentNanos = tick.TargetPresentNanos + (millis * 1_000_000L) };

    public static TheoryData<string> MovingStyles() => new() { "ripple", "draw", "sweep" };

    [Theory]
    [MemberData(nameof(MovingStyles))]
    public void A_reveal_keeps_the_point_it_started_from_through_a_drop_and_a_regrab(string style)
    {
        var settings = Settings(TinyComp.CanvasSetting.GridAnimationFromName(style)!.Value);
        var reveal = new TinyComp.GridRevealState();
        reveal.Show(300, 20, settings);
        var tick = First;
        _ = reveal.Step(tick, settings);
        tick = After(tick, 100);
        _ = reveal.Step(tick, settings);
        var showing = reveal.RevealFor(settings);
        Assert.Equal((300.0, 20.0), (showing.OriginX, showing.OriginY));

        reveal.Drop(settings);
        tick = After(tick, 50);
        _ = reveal.Step(tick, settings);
        var hiding = reveal.RevealFor(settings);
        Assert.True(hiding.Progress is > 0f and < 1f);
        Assert.Equal((300.0, 20.0), (hiding.OriginX, hiding.OriginY));

        reveal.Show(900, 650, settings);
        var regrabbed = reveal.RevealFor(settings);
        Assert.Equal(hiding.Progress, regrabbed.Progress);
        Assert.Equal((300.0, 20.0), (regrabbed.OriginX, regrabbed.OriginY));
    }

    [Theory]
    [MemberData(nameof(MovingStyles))]
    public void A_reveal_that_hid_completely_starts_from_the_next_grab(string style)
    {
        var settings = Settings(TinyComp.CanvasSetting.GridAnimationFromName(style)!.Value);
        var reveal = new TinyComp.GridRevealState();
        reveal.Show(300, 20, settings);
        var tick = First;
        _ = reveal.Step(tick, settings);
        reveal.Drop(settings);
        for (var i = 0; i < 40 && reveal.Running; i++)
        {
            tick = After(tick, 16);
            _ = reveal.Step(tick, settings);
        }

        Assert.False(reveal.Shown);
        reveal.Show(300, 700, settings);
        var next = reveal.RevealFor(settings);
        Assert.Equal((300.0, 700.0), (next.OriginX, next.OriginY));
    }
}
