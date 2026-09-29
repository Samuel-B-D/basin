using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Basin.AppMenu;
using System7.Avalonia;
using System7.Avalonia.Controls;
using System7.Avalonia.Rendering;
using System7Wm;
using Xunit;

namespace System7Wm.Tests;

public sealed class ViewTests
{
    private static Window Host(Control content, double width, double height, System7ColorDepth depth = System7ColorDepth.Indexed8)
    {
        var window = new Window { Content = content, Width = width, Height = height, SizeToContent = SizeToContent.Manual };
        System7Theme.SetColorDepth(window, depth);
        window.Show();
        Pump();
        return window;
    }

    private static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static uint[] Pixels(Window window, out int width)
    {
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame was rendered");
        width = frame.PixelSize.Width;
        var pixels = new uint[width * frame.PixelSize.Height];
        unsafe
        {
            fixed (uint* into = pixels)
            {
                frame.CopyPixels(new PixelRect(frame.PixelSize), (nint)into, pixels.Length * 4, width * 4);
            }
        }

        if (Environment.GetEnvironmentVariable("SYSTEM7_DUMP") is { } dump)
        {
            File.WriteAllBytes(Path.Combine(dump, $"view-{width}x{frame.PixelSize.Height}-{Environment.TickCount64}.raw"), System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray());
        }

        return pixels;
    }

    private static bool IsBlack(uint argb) => (argb & 0xffffff) == 0;

    private static bool IsDark(uint argb) => ((argb >> 16) & 0xff) < 0x80 && ((argb >> 8) & 0xff) < 0x80 && (argb & 0xff) < 0x80;

    private static bool IsWhite(uint argb) => (argb & 0xffffff) == 0xffffff;

    [AvaloniaTheory]
    [InlineData(System7ColorDepth.Monochrome)]
    [InlineData(System7ColorDepth.Indexed8)]
    public void The_title_strip_is_nineteen_pixels_with_the_title_line_at_its_foot(System7ColorDepth depth)
    {
        var frame = new System7WindowFrame
        {
            IsTitleStrip = true,
            Kind = System7WindowKind.Document,
            Title = "Untitled",
            IsActive = true,
            CanClose = true,
            CanZoom = true,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        var window = Host(new Panel { Background = Avalonia.Media.Brushes.White, Children = { frame } }, 203, 40, depth);

        Assert.Equal(System7WindowFrame.StripHeight, (int)frame.Bounds.Height);
        var pixels = Pixels(window, out var width);
        Assert.True(IsBlack(pixels[5]), "the top border is black");
        Assert.True(IsBlack(pixels[(18 * width) + 100]), "the title line is black");
        Assert.True(IsWhite(pixels[(19 * width) + 100]), "nothing draws below the strip");
        var stripes = Enumerable.Range(2, 15).Count(y => IsDark(pixels[(y * width) + 30]));
        Assert.True(stripes >= 5, $"the active strip carries its stripes ({stripes} black rows)");
        window.Close();
    }

    [AvaloniaFact]
    public void An_inactive_title_strip_has_no_stripes()
    {
        var frame = new System7WindowFrame
        {
            IsTitleStrip = true,
            Kind = System7WindowKind.Document,
            Title = "Untitled",
            IsActive = false,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        var window = Host(new Panel { Background = Avalonia.Media.Brushes.White, Children = { frame } }, 203, 40);

        var pixels = Pixels(window, out var width);
        var stripes = Enumerable.Range(2, 15).Count(y => IsDark(pixels[(y * width) + 30]));
        Assert.Equal(0, stripes);
        Assert.True(IsDark(pixels[(18 * width) + 100]), "the title line stays");
        window.Close();
    }

    [AvaloniaFact]
    public void The_grow_box_draws_in_fifteen_pixels()
    {
        var grow = new System7GrowBox();
        var window = Host(new Canvas { Background = Avalonia.Media.Brushes.White, Children = { grow } }, 30, 30);

        Assert.Equal(System7GrowBox.Size, (int)grow.Bounds.Width);
        Assert.Equal(System7GrowBox.Size, (int)grow.Bounds.Height);
        var pixels = Pixels(window, out var width);
        var black = 0;
        for (var y = 0; y < 15; y++)
        {
            for (var x = 0; x < 15; x++)
            {
                black += IsBlack(pixels[(y * width) + x]) ? 1 : 0;
            }
        }

        Assert.InRange(black, 20, 200);
        Assert.True(IsWhite(pixels[(20 * width) + 20]), "nothing draws outside the box");
        window.Close();
    }

    [Fact]
    public void Control_plus_a_letter_shows_as_command_and_anything_else_is_hidden()
    {
        Assert.Equal(new KeyGesture(Key.Q, KeyModifiers.Meta), MenuBar.GestureOf(new AppMenuShortcut(AppMenuModifiers.Control, "q")));
        Assert.Equal(new KeyGesture(Key.D7, KeyModifiers.Meta), MenuBar.GestureOf(new AppMenuShortcut(AppMenuModifiers.Control, "7")));
        Assert.Null(MenuBar.GestureOf(new AppMenuShortcut(AppMenuModifiers.Control | AppMenuModifiers.Shift, "q")));
        Assert.Null(MenuBar.GestureOf(new AppMenuShortcut(AppMenuModifiers.Alt, "q")));
        Assert.Null(MenuBar.GestureOf(new AppMenuShortcut(AppMenuModifiers.Control, "F5")));
        Assert.Null(MenuBar.GestureOf(null));
    }

    [AvaloniaFact]
    public void A_menu_model_maps_onto_menu_items()
    {
        var source = new RecordingSource();
        var quit = new AppMenuItem { Id = 4, Label = "Quit", Shortcut = new AppMenuShortcut(AppMenuModifiers.Control, "q") };
        var file = new AppMenuItem
        {
            Id = 1,
            Label = "File",
            IsSubmenu = true,
            Children =
            {
                new AppMenuItem { Id = 2, Label = "Open…", Enabled = false },
                new AppMenuItem { Id = 3, Kind = AppMenuItemKind.Separator },
                new AppMenuItem { Id = 5, Label = "Wrap", Kind = AppMenuItemKind.Check, ToggleState = true },
                new AppMenuItem { Id = 6, Label = "Hidden", Visible = false },
                new AppMenuItem { Id = 7, Label = "Recent", IsSubmenu = true },
                quit,
            },
        };
        source.Menu.Replace([file]);

        var item = MenuBar.Build(file, source);
        var children = item.Items.OfType<MenuItem>().ToList();
        Assert.Equal("File", item.Header);
        Assert.Equal(["Open…", "-", "Wrap", "Recent", "Quit"], children.Select(c => (string)c.Header!));
        Assert.False(children[0].IsEnabled);
        Assert.Equal(MenuItemToggleType.CheckBox, children[2].ToggleType);
        Assert.True(children[2].IsChecked);
        Assert.Single(children[3].Items);
        Assert.False(((MenuItem)children[3].Items[0]!).IsEnabled);
        Assert.Equal(new KeyGesture(Key.Q, KeyModifiers.Meta), children[4].InputGesture);

        children[4].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent, children[4]));
        Assert.Same(quit, source.Activated);
    }

    [AvaloniaFact]
    public void A_finder_window_draws_its_scroll_bars_on_the_frame()
    {
        var folder = Directory.CreateTempSubdirectory("system7-finder-");
        try
        {
            var generic = System7IconFamily.Generic("document") ?? System7IconFamily.Generic("folder")!;
            var view = new FinderView(folder.FullName, "icon", _ => generic);
            view.SetEntries(Enumerable.Range(0, 40).Select(i =>
                new FinderEntry(Path.Combine(folder.FullName, $"f{i}"), $"f{i}", false, 1, DateTime.UnixEpoch, "Plain text")));
            var frame = new System7WindowFrame
            {
                Kind = System7WindowKind.Document,
                Title = "Finder",
                CanClose = true,
                CanZoom = true,
                CanResize = true,
                HasScrollBars = true,
                Content = view,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            };
            var window = Host(frame, 363, 241);
            Pump();

            var bars = window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>().ToList();
            var vertical = bars.Single(b => b.Orientation == Avalonia.Layout.Orientation.Vertical);
            var horizontal = bars.Single(b => b.Orientation == Avalonia.Layout.Orientation.Horizontal);
            var v = new Rect(vertical.TranslatePoint(default, frame)!.Value, vertical.Bounds.Size);
            var h = new Rect(horizontal.TranslatePoint(default, frame)!.Value, horizontal.Bounds.Size);
            Assert.Equal(new Rect(346, v.Y, 16, 225 - v.Y), v);
            Assert.Equal(new Rect(0, 224, 347, 16), h);

            var pixels = Pixels(window, out var width);
            Assert.False(IsBlack(pixels[(150 * width) + 345]), "one line between the list and the bar");
            Assert.True(IsBlack(pixels[(150 * width) + 361]), "the bar's edge is the frame's");
            var box = 0;
            for (var y = 225; y < 239; y++)
            {
                for (var x = 347; x < 361; x++)
                {
                    box += IsBlack(pixels[(y * width) + x]) || !IsWhite(pixels[(y * width) + x]) ? 1 : 0;
                }
            }

            Assert.True(box > 20, "the grow box shows in the corner");
            window.Close();
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [AvaloniaFact]
    public void New_icons_take_free_cells_and_clean_up_snaps_to_the_grid()
    {
        var generic = System7IconFamily.Generic("document")!;
        var view = new FinderView("/nowhere", "icon", _ => generic) { LayoutWidth = 360 };
        view.SetPositions("icon", [new("b", new Point(300, 70))]);
        view.SetEntries(
        [
            new FinderEntry("/nowhere/a", "a", false, 1, DateTime.UnixEpoch, "Plain text"),
            new FinderEntry("/nowhere/b", "b", false, 1, DateTime.UnixEpoch, "Plain text"),
            new FinderEntry("/nowhere/c", "c", false, 1, DateTime.UnixEpoch, "Plain text"),
        ]);

        Assert.Equal(new Point(0, 0), view.Positions["a"]);
        Assert.Equal(new Point(300, 70), view.Positions["b"]);
        Assert.Equal(new Point(112, 0), view.Positions["c"]);

        var arranged = 0;
        view.Arranged += () => arranged++;
        view.CleanUp();
        Assert.Equal(new Point(336, 64), view.Positions["b"]);
        Assert.Equal(new Point(112, 0), view.Positions["c"]);
        Assert.Equal(1, arranged);

        view.RenamePosition("c", "d");
        view.SetEntries([new FinderEntry("/nowhere/d", "d", false, 1, DateTime.UnixEpoch, "Plain text")]);
        Assert.Equal(["d"], view.Positions.Keys);
        Assert.Equal(new Point(112, 0), view.Positions["d"]);

        view.SetMode("small");
        Assert.Equal(new Point(0, 0), view.Positions["d"]);
        view.SetMode("name");
        Assert.Empty(view.Positions);
        Assert.Equal(new Point(112, 0), view.PositionsOf("icon")["d"]);
    }

    [AvaloniaFact]
    public void A_selected_small_icon_reverses_only_its_name()
    {
        var generic = System7IconFamily.Generic("document")!;
        var view = new FinderView("/nowhere", "small", _ => generic) { LayoutWidth = 300 };
        view.SetEntries([new FinderEntry("/nowhere/abc", "abc", false, 1, DateTime.UnixEpoch, "Plain text")]);
        var window = Host(view, 300, 200, System7ColorDepth.Monochrome);
        view.SelectAll();
        Pump();

        var item = window.GetVisualDescendants().OfType<ListBoxItem>().Single();
        var band = item.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "Band");
        var origin = band.TranslatePoint(default, window)!.Value;
        var pixels = Pixels(window, out var width);
        var y = (int)origin.Y + 8;
        Assert.True(IsBlack(pixels[(y * width) + (int)origin.X]), "the band behind the name is reversed");
        Assert.True(IsWhite(pixels[(y * width) + (int)(origin.X + band.Bounds.Width + 20)]), "the paper past the name stays");
        Assert.True(band.Bounds.Width < 60, $"the band hugs the name: {band.Bounds.Width}");
        window.Close();
    }

    [Fact]
    public void The_alert_sounds_are_the_system_seven_set()
    {
        Assert.Equal(["Simple Beep", "Droplet", "Indigo", "Quack", "Sosumi", "Wild Eep"], System7AlertSound.All.Select(s => s.Name));
        var beep = System7AlertSound.SimpleBeep;
        Assert.InRange(beep.SampleRate, 22254, 22255);
        Assert.InRange(beep.Samples.Length / beep.SampleRate, 0.2, 0.3);
        var wav = beep.ToWav();
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal(44 + beep.Samples.Length, wav.Length);
        Assert.Contains(System7AlertSound.Find("quack")!.Samples.ToArray(), sample => sample != 128);
    }

    [Fact]
    public void Xwayland_is_on_unless_the_config_turns_it_off()
    {
        var folder = Directory.CreateTempSubdirectory("system7-config-");
        try
        {
            var log = Basin.Diagnostics.BasinLog.For("test");
            Assert.True(System7Config.Load(null, none: true, log).XWayland);
            var file = Path.Combine(folder.FullName, "system7-wm.toml");
            File.WriteAllText(file, "[windows]\nxwayland = false\n");
            var off = System7Config.Load(file, none: false, log);
            Assert.False(off.XWayland);
            Assert.Equal(["windows.xwayland"], System7Config.Load(null, none: true, log).RestartKeysChanged(off));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_folder_state_file_round_trips()
    {
        var state = Directory.CreateTempSubdirectory("system7-finder-state-");
        var previous = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        Environment.SetEnvironmentVariable("XDG_STATE_HOME", state.FullName);
        try
        {
            const string folder = "/some/where \"quoted\"";
            var saved = new FinderState { Window = new Basin.Box(40, 60, 360, 220), View = "name" };
            saved.Icons["a.txt"] = new Point(112, 64);
            saved.Icons["with space"] = new Point(0, 0);
            saved.SmallIcons["a.txt"] = new Point(128, 20);
            saved.Save(folder);

            var loaded = FinderState.Load(folder);
            Assert.Equal(new Basin.Box(40, 60, 360, 220), loaded.Window);
            Assert.Equal("name", loaded.View);
            Assert.Equal(saved.Icons, loaded.Icons);
            Assert.Equal(saved.SmallIcons, loaded.SmallIcons);
            Assert.Empty(FinderState.Load("/some/other").Icons);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_STATE_HOME", previous);
            state.Delete(recursive: true);
        }
    }

    [AvaloniaFact]
    public void The_finder_list_shows_its_entries()
    {
        var folder = Directory.CreateTempSubdirectory("system7-finder-");
        try
        {
            var generic = System7IconFamily.Generic("document") ?? System7IconFamily.Generic("folder")!;
            var view = new FinderView(folder.FullName, "name", _ => generic);
            view.SetEntries(
            [
                new FinderEntry(Path.Combine(folder.FullName, "b"), "b", true, 0, DateTime.UnixEpoch, "folder"),
                new FinderEntry(Path.Combine(folder.FullName, "a.txt"), "a.txt", false, 3000, DateTime.UnixEpoch, "Plain text"),
            ]);
            var window = Host(view, 300, 200);
            Pump();

            Assert.Equal(2, view.Entries.Count);
            _ = Pixels(window, out _);
            var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.True(texts.Contains("a.txt"), $"{view.Bounds} {window.GetVisualDescendants().OfType<ListBoxItem>().Count()} {window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault()?.Bounds} [{string.Join(",", texts)}]");
            Assert.Contains("b", texts);
            Assert.Equal("3K", FinderView.FormatSize(3000));
            Assert.Equal("512", FinderView.FormatSize(512));
            window.Close();
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [AvaloniaFact]
    public void The_rename_box_draws_no_scroll_bar()
    {
        var box = DesktopView.CreateRenameBox("a.txt", 80);
        var window = Host(new Canvas { Background = Avalonia.Media.Brushes.White, Children = { box } }, 100, 30);
        box.Focus();
        box.SelectAll();
        Pump();

        Assert.Equal(18, box.Bounds.Height);
        Assert.DoesNotContain(box.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>(), bar => bar.IsEffectivelyVisible && bar.Bounds.Width > 0);
        var pixels = Pixels(window, out var width);
        Assert.True(IsBlack(pixels[(8 * width) + 79]), "the right border is drawn at the box's edge");
        window.Close();
    }

    private sealed class RecordingSource : IAppMenuSource
    {
        public AppMenuAddressKind Kind => AppMenuAddressKind.DbusMenu;

        public AppMenu Menu { get; } = new();

        public bool IsReady => true;

        public AppMenuItem? Activated { get; private set; }

        public event Action? Failed
        {
            add { }
            remove { }
        }

        public void AboutToShow(AppMenuItem item)
        {
        }

        public void Activate(AppMenuItem item, uint timestamp) => Activated = item;

        public void Dispose()
        {
        }
    }
}
