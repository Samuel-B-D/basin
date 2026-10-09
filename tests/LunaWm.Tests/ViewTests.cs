using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Xunit;

namespace LunaWm.Tests;

public sealed class ViewTests
{
    private static Window Host(Control content, double width, double height)
    {
        var window = new Window { Content = content, Width = width, Height = height, Background = Avalonia.Media.Brushes.Black };
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

    private static int Bright(Window window, Control glyph)
    {
        Pump();
        using var frame = window.CaptureRenderedFrame()!;
        var origin = glyph.TranslatePoint(default, window)!.Value;
        var box = new PixelRect((int)origin.X, (int)origin.Y, (int)glyph.Bounds.Width, (int)glyph.Bounds.Height);
        using var buffer = frame.Lock();
        var count = 0;
        unsafe
        {
            for (var y = box.Y; y < box.Bottom; y++)
            {
                var row = (byte*)buffer.Address + (y * buffer.RowBytes);
                for (var x = box.X; x < box.Right; x++)
                {
                    var pixel = row + (x * 4);
                    if (pixel[0] > 200 && pixel[1] > 200 && pixel[2] > 200)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    private static T Find<T>(Control root) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().First();

    [AvaloniaFact]
    public void The_battery_fills_by_level_and_charging_adds_the_bolt()
    {
        var model = new StatusBarModel { BatteryShown = true, BatteryLevel = 0, Clock = "12:00 PM" };
        var view = new StatusBarView { DataContext = model };
        var window = Host(view, 1024, 28);
        var glyph = Find<BatteryGlyph>(view);
        var empty = Bright(window, glyph);
        model.BatteryLevel = 5;
        var half = Bright(window, glyph);
        model.BatteryLevel = 11;
        var full = Bright(window, glyph);
        model.BatteryCharging = true;
        var charging = Bright(window, glyph);
        Assert.True(empty < half && half < full, $"{empty} {half} {full}");
        Assert.InRange(full, 11 * 16, (11 * 16) + 20);
        Assert.NotEqual(full, charging);
    }

    [AvaloniaFact]
    public void Wifi_shows_its_bars_and_bluetooth_its_connection()
    {
        var model = new StatusBarModel { WifiShown = true, WifiBars = 0, BluetoothShown = true, Clock = "12:00 PM" };
        var view = new StatusBarView { DataContext = model };
        var window = Host(view, 1024, 28);
        var wifi = Find<WifiGlyph>(view);
        var bars = new int[4];
        for (var i = 0; i <= 3; i++)
        {
            model.WifiBars = i;
            bars[i] = Bright(window, wifi);
        }

        Assert.True(bars[0] < bars[1] && bars[1] < bars[2] && bars[2] < bars[3], string.Join(' ', bars));
        var bluetooth = Find<BluetoothGlyph>(view);
        var paired = Bright(window, bluetooth);
        model.BluetoothConnected = true;
        Assert.True(Bright(window, bluetooth) > paired);
    }

    [AvaloniaFact]
    public void Hidden_indicators_collapse_and_the_title_hides_when_locked()
    {
        var model = new StatusBarModel { Title = "Calendar", Clock = "12:00 PM" };
        var view = new StatusBarView { DataContext = model };
        _ = Host(view, 1024, 28);
        Assert.False(Find<WifiGlyph>(view).IsEffectivelyVisible);
        Assert.False(Find<BluetoothGlyph>(view).IsEffectivelyVisible);
        Assert.False(Find<VolumeGlyph>(view).IsEffectivelyVisible);
        Assert.True(view.FindControl<Border>("TitleHit")!.IsVisible);
        model.Locked = true;
        Pump();
        Assert.False(view.FindControl<Border>("TitleHit")!.IsVisible);
    }

    [AvaloniaFact]
    public void A_system_menu_lists_its_rows_and_a_toggle_reports_the_change()
    {
        var model = new SystemMenuModel { PanelWidth = 300, PanelLeft = 700 };
        var changes = new List<bool>();
        var toggle = new ToggleRow { Key = "wifi", Label = "Wi-Fi", On = true, Changed = changes.Add };
        model.Rows.Add(toggle);
        model.Rows.Add(new DividerRow());
        model.Rows.Add(new ActionRow { Label = "Home", Detail = "80%", Checked = true });
        model.Rows.Add(new NoteRow { Label = "Use your network settings to join a new secured network." });
        var view = new SystemMenuView { DataContext = model };
        var window = Host(view, 1024, 740);
        var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Wi-Fi", texts);
        Assert.Contains("On", texts);
        Assert.Contains("Home", texts);
        var panel = view.FindControl<Control>("Panel")!;
        Assert.Equal(700, panel.TranslatePoint(default, window)!.Value.X, 1);

        toggle.Toggle();
        Pump();
        Assert.Equal([false], changes);
        Assert.Contains("Off", view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
    }

    private static LauncherModel Launcher(int icons)
    {
        var model = new LauncherModel();
        var apps = new LauncherTabModel { Name = "Apps" };
        for (var i = 0; i < icons; i++)
        {
            apps.Icons.Add(new LauncherIconModel { Id = $"app{i}.desktop", Name = $"App {i}" });
        }

        model.Tabs.Add(apps);
        model.Tabs.Add(new LauncherTabModel { Name = "Settings" });
        model.Current = apps;
        return model;
    }

    [AvaloniaFact]
    public void The_launcher_grid_holds_seven_icons_to_a_row()
    {
        var model = Launcher(10);
        var view = new LauncherView { DataContext = model };
        var window = Host(view, 1024, 740);
        var grid = view.FindControl<ItemsControl>("Grid")!;
        var first = grid.ContainerFromIndex(0)!.TranslatePoint(default, window)!.Value;
        var seventh = grid.ContainerFromIndex(6)!.TranslatePoint(default, window)!.Value;
        var eighth = grid.ContainerFromIndex(7)!.TranslatePoint(default, window)!.Value;
        Assert.Equal(first.Y, seventh.Y);
        Assert.True(eighth.Y > first.Y);
        Assert.Equal(first.X, eighth.X, 1);
        Assert.Equal(27, first.X, 1);
    }

    [AvaloniaFact]
    public void The_launcher_scroll_fades_are_dark_and_fade_rather_than_pop()
    {
        var model = Launcher(60);
        var view = new LauncherView { DataContext = model };
        _ = Host(view, 1024, 740);
        Pump();
        var scroller = view.FindControl<ScrollViewer>("Scroller")!;
        var top = scroller.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_FadeTop");
        var bottom = scroller.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_FadeBottom");
        Assert.True(top.IsVisible && bottom.IsVisible, $"top={top.IsVisible} {top.Opacity} bottom={bottom.IsVisible} {bottom.Opacity} classes={string.Join(",", scroller.Classes)}");
        var edge = ((Avalonia.Media.LinearGradientBrush)bottom.Background!).GradientStops[1].Color;
        Assert.True(edge.R < 0x40 && edge.G < 0x40 && edge.B < 0x40, edge.ToString());
        Settle();
        Assert.Equal(0, top.Opacity);
        Assert.Equal(1, bottom.Opacity);

        scroller.Offset = new Vector(0, 200);
        Pump();
        Assert.True(top.Opacity < 1, $"top fade popped to {top.Opacity}");
        Settle();
        Assert.Equal(1, top.Opacity);
    }

    private static void Settle()
    {
        var until = DateTime.UtcNow.AddMilliseconds(400);
        while (DateTime.UtcNow < until)
        {
            Pump();
            Thread.Sleep(10);
        }
    }

    [AvaloniaFact]
    public void Reorder_moves_icons_within_and_between_tabs_and_hides_with_the_badge()
    {
        var model = Launcher(4);
        var moves = new List<string>();
        model.Moved += (icon, tab, index) => moves.Add($"{icon.Id}>{tab}:{index}");
        var hidden = new List<string>();
        model.Hidden += icon => hidden.Add(icon.Id);
        var view = new LauncherView { DataContext = model };
        _ = Host(view, 1024, 740);
        model.Reordering = true;
        Pump();
        Assert.True(model.Tabs[0].Icons.All(icon => icon.Reordering));

        var icon = model.Tabs[0].Icons[0];
        model.MoveWithin(icon, 2);
        Assert.Equal(["app1.desktop", "app2.desktop", "app0.desktop", "app3.desktop"], model.Tabs[0].Icons.Select(i => i.Id));
        model.MoveToTab(icon, model.Tabs[1]);
        Assert.Single(model.Tabs[1].Icons);
        model.Hide(model.Tabs[0].Icons[0]);
        Assert.Equal(["app0.desktop>Apps:2", "app0.desktop>Settings:0"], moves);
        Assert.Equal(["app1.desktop"], hidden);

        var launched = new List<string>();
        model.Launched += i => launched.Add(i.Id);
        model.Launch(model.Tabs[0].Icons[0]);
        Assert.Empty(launched);
        model.Reordering = false;
        model.Launch(model.Tabs[0].Icons[0]);
        Assert.Single(launched);
    }

    [AvaloniaFact]
    public void The_dock_slots_are_centered_with_the_launcher_button_last()
    {
        var model = new QuickLaunchModel();
        model.Icons.Add(new LauncherIconModel { Id = "a.desktop", Name = "A" });
        model.Icons.Add(new LauncherIconModel { Id = "b.desktop", Name = "B" });
        Assert.Equal(0, model.SlotAt(400, 1024));
        Assert.Equal(1, model.SlotAt(512, 1024));
        Assert.Equal(2, model.SlotAt(620, 1024));
        Assert.Equal(-1, model.SlotAt(100, 1024));
    }

    [AvaloniaFact]
    public void A_dashboard_row_is_fifty_two_pixels_with_a_count_and_expands_its_stack()
    {
        var model = new DashboardModel();
        var group = new DashboardGroupModel { Key = "mail", AppName = "Mail", Summary = "Ada", Body = "Notes" };
        group.Items.Add(new DashboardItemModel { Id = 2, Summary = "Ada", Body = "Notes" });
        group.Items.Add(new DashboardItemModel { Id = 1, Summary = "Charles" });
        group.Count = 2;
        model.Groups.Add(group);
        model.Refresh();
        var view = new DashboardView { DataContext = model };
        var window = Host(view, 1024, 740);
        var row = view.GetVisualDescendants().OfType<AvaEnyo.Controls.SwipeableItem>().First();
        Assert.Equal(52, row.Bounds.Height);
        Assert.Equal(1024 - 320, view.FindControl<Border>("Column")!.TranslatePoint(default, window)!.Value.X, 1);
        Assert.Contains("2", view.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text));
        Assert.DoesNotContain("Charles", view.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text));
        group.Expanded = true;
        Pump();
        Assert.Contains("Charles", view.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text));
    }

    [AvaloniaFact]
    public void A_banner_replaces_the_notification_icons_and_a_long_one_scrolls()
    {
        var model = new StatusBarModel { Clock = "12:00 PM" };
        model.NotificationIcons.Add(new StatusIconModel { Key = "mail", Letter = "M" });
        var view = new StatusBarView { DataContext = model };
        _ = Host(view, 1024, 28);
        Assert.True(view.FindControl<Border>("NotificationHit")!.IsVisible);
        model.Banner = "Short";
        model.BannerShown = true;
        Pump();
        Assert.False(view.FindControl<Border>("NotificationHit")!.IsVisible);
        Assert.False(view.Scrolling);
        model.Banner = new string('x', 300);
        Pump();
        Assert.True(view.Scrolling);
    }

    [AvaloniaFact]
    public void The_lock_shows_its_clock_and_a_pin_pad_takes_digits_and_backspace()
    {
        var model = new LockScreenModel { Clock = "12:00 PM", Date = "Monday, January 2", Pin = true };
        var submitted = 0;
        model.Submitted += () => submitted++;
        var view = new LockScreenView { DataContext = model };
        _ = Host(view, 1024, 740);
        Assert.Equal("12:00 PM", view.FindControl<TextBlock>("ClockText")!.Text);
        Assert.True(view.FindControl<Border>("Ring")!.IsVisible);
        Assert.False(view.FindControl<Border>("Pad")!.IsVisible);
        model.PadShown = true;
        Pump();
        Assert.False(view.FindControl<Border>("Ring")!.IsVisible);
        var keys = view.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("lockkey")).ToList();
        Assert.Equal(12, keys.Count);
        keys.First(k => (string?)k.Tag == "4").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        keys.First(k => (string?)k.Tag == "2").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        keys.First(k => (string?)k.Tag == "\u232b").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        keys.First(k => (string?)k.Tag == "7").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("47", model.Entry);
        Assert.Equal("\u25cf\u25cf", model.Dots);
        keys.First(k => (string?)k.Tag == "\u21b5").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, submitted);
        Assert.False(view.FindControl<TextBox>("PasswordBox")!.IsVisible);
    }

    [AvaloniaFact]
    public void Just_type_groups_results_under_dividers_and_moves_the_selection_past_them()
    {
        var model = new JustTypeModel();
        model.SetRows(
        [
            new JustTypeRow { Kind = "header", Label = "Apps", IsHeader = true },
            new JustTypeRow { Kind = "app", Label = "Sketchbook" },
            new JustTypeRow { Kind = "header", Label = "Actions", IsHeader = true },
            new JustTypeRow { Kind = "run", Label = "Run \"s\"" },
        ]);
        var view = new JustTypeView { DataContext = model };
        _ = Host(view, 1024, 740);
        Assert.Equal(2, view.GetVisualDescendants().OfType<AvaEnyo.Controls.Divider>().Count(d => d.IsEffectivelyVisible));
        Assert.Equal("Sketchbook", model.SelectedRow!.Label);
        model.Move(1);
        Assert.Equal("Run \"s\"", model.SelectedRow!.Label);
        model.Move(1);
        Assert.Equal("Sketchbook", model.SelectedRow!.Label);
        var chosen = new List<string>();
        model.Chosen += row => chosen.Add(row.Kind);
        model.ChooseSelected();
        Assert.Equal(["app"], chosen);
    }

    [AvaloniaFact]
    public unsafe void Notification_image_data_converts_to_premultiplied_bgra()
    {
        byte[] pixels = [255, 0, 0, 255, 0, 255, 0, 128, 7, 7, 7, 7, 10, 20, 30, 255, 40, 50, 60, 0, 7, 7, 7, 7];
        var image = new Basin.Notifications.NotificationImage(2, 2, 12, true, 8, 4, pixels);
        using var bitmap = NotificationImages.Convert(image)!;
        Assert.Equal(new PixelSize(2, 2), bitmap.PixelSize);
        using var frame = bitmap.Lock();
        var row0 = (byte*)frame.Address;
        Assert.Equal([0, 0, 255, 255], new[] { row0[0], row0[1], row0[2], row0[3] });
        Assert.Equal([0, 128, 0, 128], new[] { row0[4], row0[5], row0[6], row0[7] });
        var row1 = (byte*)frame.Address + frame.RowBytes;
        Assert.Equal([30, 20, 10, 255], new[] { row1[0], row1[1], row1[2], row1[3] });
        Assert.Null(NotificationImages.Convert(image with { Channels = 3 }));
        Assert.Null(NotificationImages.Convert(image with { Pixels = pixels.AsMemory(0, 10) }));
    }
}
