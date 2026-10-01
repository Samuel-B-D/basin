using Basin.Capabilities;
using Basin.Diagnostics;
using Basin.Frames.MsStyles;
using Basin.UI.Skia;
using Xunit;

namespace Basin.Tests;

public sealed class MsStylesFadeTests
{
    private const long Ms = 1_000_000;
    private const long ChangedAt = 5_000 * Ms;

    private static FrameInteraction HoverIn(long elapsed) => new(FramePart.Close, FramePart.None)
    {
        PreviousHot = FramePart.Title,
        HoverChangedAt = ChangedAt,
        Now = ChangedAt + elapsed,
    };

    private static FrameInteraction HoverOut(long elapsed) => new(FramePart.Title, FramePart.None)
    {
        PreviousHot = FramePart.Close,
        HoverChangedAt = ChangedAt,
        Now = ChangedAt + elapsed,
    };

    public static TheoryData<string, long, string> Goldens => new()
    {
        { "in", 0, "msstyles-glassy-fade-in-0" },
        { "in", 40 * Ms, "msstyles-glassy-fade-in-40" },
        { "in", 80 * Ms, "msstyles-glassy-fade-in-80" },
        { "out", 0, "msstyles-glassy-fade-out-0" },
        { "out", 82 * Ms, "msstyles-glassy-fade-out-82" },
        { "out", 165 * Ms, "msstyles-glassy-fade-out-165" },
    };

    [Theory]
    [MemberData(nameof(Goldens))]
    public void Hover_fade_matches_golden(string direction, long elapsed, string golden)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "these goldens carry shaped text, and glyph rasterization belongs to the host's Skia build");
        using var rig = new MsStylesRig("Glassy", MsStylesLook.Glass);
        var interaction = direction == "in" ? HoverIn(elapsed) : HoverOut(elapsed);
        var rgba = Render(rig, MsStylesRig.StateFor("active"), interaction, out var width, out var height);
        Golden.AssertMatches(rgba, width, height, golden);
    }

    [Fact]
    public void A_fade_starts_at_the_old_state_and_ends_at_the_new_one()
    {
        using var rig = new MsStylesRig("Glassy", MsStylesLook.Glass);
        var state = MsStylesRig.StateFor("active");
        var normal = Render(rig, state, default, out _, out _);
        var hot = Render(rig, state, new FrameInteraction(FramePart.Close, FramePart.None), out _, out _);

        Assert.NotEqual(normal, hot);
        Assert.Equal(normal, Render(rig, state, HoverIn(0), out _, out _));
        Assert.Equal(hot, Render(rig, state, HoverIn(VistaTransitions.HoverOutNanos), out _, out _));
        Assert.Equal(hot, Render(rig, state, HoverOut(0), out _, out _));
        Assert.Equal(normal, Render(rig, state, HoverOut(VistaTransitions.HoverOutNanos), out _, out _));
    }

    [Fact]
    public void The_hot_state_arrives_before_the_old_state_leaves()
    {
        Assert.Equal(1f, VistaTransitions.FadeIn(80 * Ms));
        Assert.Equal(0.5f, VistaTransitions.FadeIn(40 * Ms));
        Assert.Equal(0f, VistaTransitions.FadeIn(0));
        Assert.Equal(1f, VistaTransitions.FadeOut(0));
        Assert.InRange(VistaTransitions.FadeOut(80 * Ms), 0.51f, 0.52f);
        Assert.Equal(0f, VistaTransitions.FadeOut(165 * Ms));
    }

    [Fact]
    public void Animating_holds_for_the_slow_fade_and_then_stops()
    {
        using var rig = new MsStylesRig("Glassy", MsStylesLook.Glass);
        var renderer = rig.Renderer();
        var state = MsStylesRig.StateFor("active");
        Render(renderer, state, default, out _, out _);

        Assert.True(renderer.Animating(state, HoverIn(0)));
        Assert.True(renderer.Animating(state, HoverIn(100 * Ms)));
        Assert.False(renderer.Animating(state, HoverIn(165 * Ms)));
        Assert.True(renderer.Animating(state, HoverOut(164 * Ms)));
        Assert.False(renderer.Animating(state, HoverOut(165 * Ms)));
        Assert.False(renderer.Animating(state, default));
    }

    [Fact]
    public void A_press_and_a_title_hover_do_not_animate()
    {
        using var rig = new MsStylesRig("Glassy", MsStylesLook.Glass);
        var renderer = rig.Renderer();
        var state = MsStylesRig.StateFor("active");
        Render(renderer, state, default, out _, out _);

        var pressed = new FrameInteraction(FramePart.Close, FramePart.Close)
        {
            PreviousHot = FramePart.Title,
            HoverChangedAt = ChangedAt,
            PressChangedAt = ChangedAt + Ms,
            Now = ChangedAt + 2 * Ms,
        };
        var released = pressed with { Pressed = FramePart.None, PreviousPressed = FramePart.Close, PressChangedAt = ChangedAt + 3 * Ms, Now = ChangedAt + 4 * Ms };
        var title = new FrameInteraction(FramePart.Title, FramePart.None) { PreviousHot = FramePart.None, HoverChangedAt = ChangedAt, Now = ChangedAt };

        Assert.False(renderer.Animating(state, pressed));
        Assert.False(renderer.Animating(state, released));
        Assert.False(renderer.Animating(state, title));
    }

    [Theory]
    [InlineData("Glassy", MsStylesLook.Basic)]
    [InlineData("Plain", MsStylesLook.ClassicXp)]
    public void Basic_and_xp_switch_instantly(string theme, MsStylesLook look)
    {
        using var rig = new MsStylesRig(theme, look);
        var renderer = rig.Renderer();
        var state = MsStylesRig.StateFor("active");
        var hot = Render(renderer, state, new FrameInteraction(FramePart.Close, FramePart.None), out _, out _);

        Assert.False(renderer.Animating(state, HoverIn(0)));
        Assert.Equal(hot, Render(renderer, state, HoverIn(0), out _, out _));
    }

    [Fact]
    public void A_glass_button_damages_its_glow()
    {
        using var rig = new MsStylesRig("Glassy", MsStylesLook.Glass);
        var renderer = rig.Renderer();
        Render(renderer, MsStylesRig.StateFor("active"), default, out _, out _);
        var button = renderer.Painter.Geometry.BoundsOf(FramePart.Close);
        var damage = renderer.PartBounds(FramePart.Close);

        Assert.Equal((int)button.X - 9, damage.X);
        Assert.Equal((int)button.Y - 8, damage.Y);
        Assert.Equal((int)button.Width + 17, damage.Width);
        Assert.Equal((int)button.Height + 15, damage.Height);
    }

    private static byte[] Render(MsStylesRig rig, in FrameState state, in FrameInteraction interaction, out int width, out int height) =>
        Render(rig.Renderer(), state, interaction, out width, out height);

    private static byte[] Render(MsStylesFrameRenderer renderer, in FrameState state, in FrameInteraction interaction, out int width, out int height)
    {
        using var host = new SkiaUIHost();
        var insets = renderer.Measure(state, 1.0);
        var outsets = renderer.Outsets(state, 1.0);
        const int clientWidth = 200;
        const int clientHeight = 40;
        width = clientWidth + insets.Left + insets.Right + outsets.Left + outsets.Right;
        height = clientHeight + insets.Top + insets.Bottom + outsets.Top + outsets.Bottom;
        var surface = host.CreateSurface(new UISurfaceOptions { Target = UITargetKind.Memory, Width = width, Height = height, Scale = 1.0 });
        Assert.NotNull(surface);
        renderer.Draw(surface, new Box(insets.Left + outsets.Left, insets.Top + outsets.Top, clientWidth, clientHeight), state, interaction);
        Assert.True(surface.TryAcquire(out var frame));
        try
        {
            return BufferCapture.ReadRgba(frame.Buffer!);
        }
        finally
        {
            frame.Dispose();
            surface.Dispose();
        }
    }
}
