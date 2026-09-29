using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Basin;
using Basin.Pty;
using System7.Avalonia;
using System7.Avalonia.Controls;
using System7.Avalonia.Rendering;
using System7Wm;
using Xunit;

namespace System7Wm.Tests;

public sealed class TerminalViewTests
{
    private static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static (Window Window, System7WindowFrame Frame, TerminalWindow Terminal, EpollEventLoop Loop) Open(System7ColorDepth depth, int width = 363, int height = 241)
    {
        var loop = new EpollEventLoop();
        var terminal = new TerminalWindow(loop, new PtyStartInfo(["sleep", "30"]), "Terminal", 9, 100, depth);
        var frame = new System7WindowFrame
        {
            Kind = System7WindowKind.Document,
            Title = "Terminal",
            CanClose = true,
            CanZoom = true,
            CanResize = true,
            HasScrollBars = true,
            Content = terminal.View,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };
        var window = new Window { Content = frame, Width = width, Height = height, SizeToContent = SizeToContent.Manual };
        System7Theme.SetColorDepth(window, depth);
        window.Show();
        Pump();
        return (window, frame, terminal, loop);
    }

    private static void Close(Window window, TerminalWindow terminal, EpollEventLoop loop)
    {
        window.Close();
        terminal.Dispose();
        PtySession.CollectOrphans(TimeSpan.FromMilliseconds(200));
        loop.Dispose();
    }

    private static uint Pixel(Window window, int x, int y)
    {
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame was rendered");
        var pixels = new uint[frame.PixelSize.Width * frame.PixelSize.Height];
        unsafe
        {
            fixed (uint* into = pixels)
            {
                frame.CopyPixels(new PixelRect(frame.PixelSize), (nint)into, pixels.Length * 4, frame.PixelSize.Width * 4);
            }
        }

        var rgba = pixels[(y * frame.PixelSize.Width) + x];
        return 0xFF000000 | ((rgba & 0xFF) << 16) | (rgba & 0xFF00) | ((rgba >> 16) & 0xFF);
    }

    [Theory]
    [InlineData(System7ColorDepth.Monochrome)]
    [InlineData(System7ColorDepth.Indexed2)]
    [InlineData(System7ColorDepth.Indexed4)]
    [InlineData(System7ColorDepth.Indexed8)]
    [InlineData(System7ColorDepth.Rgb555)]
    [InlineData(System7ColorDepth.Rgb888)]
    public void The_palette_is_black_ink_on_white_paper_at_every_depth(System7ColorDepth depth)
    {
        Assert.Equal(TerminalPalette.Paper, TerminalPalette.Mapped(0, depth));
        Assert.Equal(TerminalPalette.Ink, TerminalPalette.Mapped(15, depth));
        for (var i = 1; i < 256; i++)
        {
            if (i == 15)
            {
                continue;
            }

            var mapped = TerminalPalette.Mapped(i, depth);
            if (depth == System7ColorDepth.Monochrome)
            {
                Assert.Equal(TerminalPalette.Ink, mapped);
            }
            else if (depth == System7ColorDepth.Rgb888)
            {
                Assert.Equal(TerminalPalette.Xterm(i), mapped);
            }
            else
            {
                Assert.Equal(DepthColors.Map(TerminalPalette.Xterm(i), depth), mapped);
            }
        }
    }

    [AvaloniaFact]
    public void The_control_reads_the_palette_and_one_bit_draws_color_as_black()
    {
        var (window, _, terminal, loop) = Open(System7ColorDepth.Monochrome);
        try
        {
            Assert.IsAssignableFrom<Brush>(terminal.Control.Resources[TerminalPalette.Key(0)]);
            terminal.Receive("\u001b[41m      \u001b[0m\r\n\u001b[7m      \u001b[0m"u8);
            Pump();
            var origin = terminal.Control.TranslatePoint(default, window)!.Value;
            var (x, y) = ((int)origin.X, (int)origin.Y);
            Assert.Equal(0xFFFFFFFFu, Pixel(window, x + 60, y + 5));
            Assert.Equal(0xFF000000u, Pixel(window, x + 15, y + 5));
            Assert.Equal(0xFF000000u, Pixel(window, x + 15, y + 16));

            terminal.SetDepth(System7ColorDepth.Indexed8);
            Pump();
            Assert.Equal(DepthColors.Map(TerminalPalette.Xterm(1), System7ColorDepth.Indexed8) | 0xFF000000, Pixel(window, x + 15, y + 5));
        }
        finally
        {
            Close(window, terminal, loop);
        }
    }

    [AvaloniaFact]
    public void The_scroll_bars_sit_on_the_frame_and_the_controls_own_bar_is_hidden()
    {
        var (window, frame, terminal, loop) = Open(System7ColorDepth.Indexed8);
        try
        {
            var bars = window.GetVisualDescendants().OfType<ScrollBar>().Where(b => b.IsEffectivelyVisible).ToList();
            var vertical = bars.Single(b => b.Orientation == Avalonia.Layout.Orientation.Vertical);
            var horizontal = bars.Single(b => b.Orientation == Avalonia.Layout.Orientation.Horizontal);
            var v = new Rect(vertical.TranslatePoint(default, frame)!.Value, vertical.Bounds.Size);
            var h = new Rect(horizontal.TranslatePoint(default, frame)!.Value, horizontal.Bounds.Size);
            Assert.Equal(new Rect(346, v.Y, 16, 225 - v.Y), v);
            Assert.Equal(new Rect(0, 224, 347, 16), h);
            Assert.False(horizontal.IsEnabled);
            Assert.Equal("TerminalVerticalScrollBar", vertical.Name);
        }
        finally
        {
            Close(window, terminal, loop);
        }
    }

    [AvaloniaFact]
    public void The_scroll_bar_follows_the_scrollback()
    {
        var (window, _, terminal, loop) = Open(System7ColorDepth.Indexed8);
        try
        {
            for (var i = 0; i < 60; i++)
            {
                terminal.Receive(System.Text.Encoding.ASCII.GetBytes($"line {i}\r\n"));
            }

            Pump();
            var vertical = window.GetVisualDescendants().OfType<ScrollBar>().Single(b => b.Name == "TerminalVerticalScrollBar");
            Assert.True(vertical.IsEnabled);
            Assert.True(vertical.Maximum > 0);
            Assert.Equal(vertical.Maximum, vertical.Value);

            var inner = terminal.Control.Children.OfType<ScrollBar>().Single();
            Assert.Equal(0, inner.Bounds.Width);
            Assert.Single(window.GetVisualDescendants().OfType<ScrollBar>(),
                b => b.Orientation == Avalonia.Layout.Orientation.Vertical && b.IsEffectivelyVisible && b.Bounds.Width > 0);

            vertical.Value = 0;
            Pump();
            Assert.Equal(0, terminal.Model.ScrollOffset);
        }
        finally
        {
            Close(window, terminal, loop);
        }
    }

    [AvaloniaFact]
    public void Option_and_a_key_sends_escape_and_the_key()
    {
        var (window, _, terminal, loop) = Open(System7ColorDepth.Indexed8);
        try
        {
            var sent = new List<byte>();
            terminal.Model.UserInput += (_, e) => sent.AddRange(e.Data.ToArray());
            terminal.Control.Focus();
            window.KeyPressQwerty(PhysicalKey.AltLeft, RawInputModifiers.Alt);
            window.KeyPressQwerty(PhysicalKey.X, RawInputModifiers.Alt);
            window.KeyReleaseQwerty(PhysicalKey.X, RawInputModifiers.Alt);
            window.KeyReleaseQwerty(PhysicalKey.AltLeft, RawInputModifiers.None);
            Pump();
            Assert.Equal(new byte[] { 0x1b, (byte)'x' }, sent);
        }
        finally
        {
            Close(window, terminal, loop);
        }
    }

    [AvaloniaFact]
    public void A_character_split_across_two_reads_is_drawn_whole()
    {
        var (window, _, terminal, loop) = Open(System7ColorDepth.Indexed8);
        try
        {
            terminal.Receive([0x63, 0x61, 0x66, 0xC3]);
            terminal.Receive([0xA9, 0x21]);
            Assert.StartsWith("café!", terminal.Model.Terminal.Engine.GetLine(0), StringComparison.Ordinal);
        }
        finally
        {
            Close(window, terminal, loop);
        }
    }

    [Theory]
    [InlineData(new byte[] { 0x61 }, 1)]
    [InlineData(new byte[] { 0x61, 0xC3 }, 1)]
    [InlineData(new byte[] { 0xC3, 0xA9 }, 2)]
    [InlineData(new byte[] { 0xE2, 0x82 }, 0)]
    [InlineData(new byte[] { 0xE2, 0x82, 0xAC }, 3)]
    [InlineData(new byte[] { 0x61, 0xF0, 0x9F, 0x98 }, 1)]
    public void The_complete_utf8_prefix_stops_before_a_split_character(byte[] data, int complete)
    {
        Assert.Equal(complete, Utf8.CompleteLength(data));
    }

    [Fact]
    public void The_terminal_keys_read_from_the_config()
    {
        var folder = Directory.CreateTempSubdirectory("system7-terminal-config-");
        try
        {
            var log = Basin.Diagnostics.BasinLog.For("test");
            var defaults = System7Config.Load(null, none: true, log);
            Assert.Equal(9, defaults.TerminalFontSize);
            Assert.Equal(1000, defaults.TerminalScrollback);
            Assert.Equal(Basin.Config.Modifiers.Super | Basin.Config.Modifiers.Alt, defaults.NewTerminal!.ModifierMask);

            var file = Path.Combine(folder.FullName, "system7-wm.toml");
            File.WriteAllText(file, "[terminal]\ncommand = \"foot\"\nshell = \"/bin/zsh\"\nfont_size = 12\nscrollback = 50\n[keybindings]\nnew_terminal = \"\"\n");
            var config = System7Config.Load(file, none: false, log);
            Assert.Equal("foot", config.TerminalCommand);
            Assert.Equal("/bin/zsh", config.TerminalShell);
            Assert.Equal(12, config.TerminalFontSize);
            Assert.Equal(50, config.TerminalScrollback);
            Assert.Null(config.NewTerminal);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void Shell_words_split_quotes_the_way_sh_does()
    {
        Assert.Equal(["sh", "-c", "echo 'a b'; exit 0"], ShellWords.Split("sh -c \"echo 'a b'; exit 0\""));
        Assert.Equal(["sh", "-c", "printf \"x\""], ShellWords.Split("sh -c 'printf \"x\"'"));
        Assert.Equal(["a b", "c"], ShellWords.Split("a\\ b c"));
    }

    [Fact]
    public void A_terminal_state_file_round_trips()
    {
        var folder = Directory.CreateTempSubdirectory("system7-terminal-state-");
        var previous = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_STATE_HOME", folder.FullName);
            new TerminalState { Window = new Box(10, 40, 499, 281), FontSize = 12 }.Save();
            var loaded = TerminalState.Load();
            Assert.Equal(new Box(10, 40, 499, 281), loaded.Window);
            Assert.Equal(12, loaded.FontSize);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_STATE_HOME", previous);
            folder.Delete(recursive: true);
        }
    }
}
