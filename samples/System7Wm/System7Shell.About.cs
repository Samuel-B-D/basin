using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Basin;
using System7.Avalonia.Controls;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal sealed partial class System7Shell
{
    private IEventSource? _aboutTimer;

    private void OpenAbout()
    {
        var rows = new StackPanel { Spacing = 2 };
        var header = new TextBlock();
        var totals = new TextBlock();
        var content = new StackPanel
        {
            Margin = new Thickness(10),
            Spacing = 6,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children =
                    {
                        new System7IconView { Icon = System7IconFamily.Generic("macintosh"), Width = 32, Height = 32 },
                        header,
                    },
                },
                totals,
                new Rectangle { Height = 1, Fill = Brushes.Black },
                rows,
            },
        };
        var window = OpenShellWindow("about", "About This Macintosh", content, 420, 200);
        void Refresh()
        {
            if (!window.Mapped)
            {
                return;
            }

            var (total, available) = MemorySample.System();
            header.Text = $"basin\nSystem Software: system7-wm\nLinux {Environment.OSVersion.Version}";
            totals.Text = $"Built-in Memory:  {total:N0}K\nLargest Unused Block:  {available:N0}K";
            rows.Children.Clear();
            var entries = new List<(string Name, System7IconFamily? Icon, long Pss, long Anon)>();
            var (selfPss, selfAnon) = MemorySample.Tree(Environment.ProcessId);
            entries.Add(("System Software", System7IconFamily.Generic("system-folder"), selfPss, selfAnon));
            foreach (var app in _apps.Values.Where(a => !a.IsFinder))
            {
                var pids = app.Windows.Select(w => w.Surface is { } s ? (int)PidOf(s) : 0).Where(p => p > 0).Distinct();
                long pss = 0;
                long anon = 0;
                foreach (var pid in pids)
                {
                    var (p, a) = MemorySample.Tree(pid);
                    pss += p;
                    anon += a;
                }

                entries.Add((app.Name, app.Icon, pss, anon));
            }

            foreach (var (name, icon, pss, anon) in entries)
            {
                var share = total > 0 ? Math.Clamp(pss / (double)total, 0, 1) : 0;
                var used = pss > 0 ? Math.Clamp(anon / (double)pss, 0, 1) : 0;
                var bar = new Grid { Width = 120, Height = 10, ColumnDefinitions = new ColumnDefinitions("*"), VerticalAlignment = VerticalAlignment.Center };
                var width = Math.Max(2, 120 * share * 4);
                bar.Children.Add(new Border { Width = Math.Min(120, width), HorizontalAlignment = HorizontalAlignment.Left, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Background = Brushes.White });
                bar.Children.Add(new Rectangle { Width = Math.Min(120, width) * used, HorizontalAlignment = HorizontalAlignment.Left, Fill = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0xcc)) });
                rows.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new System7IconView { Icon = icon?.Small ?? System7IconFamily.Generic("application")?.Small, Width = 16, Height = 16 },
                        new TextBlock { Text = name, Width = 150 },
                        new TextBlock { Text = $"{pss:N0}K", Width = 80, TextAlignment = TextAlignment.Right },
                        bar,
                    },
                });
            }

            window.ContentDip = (420, Math.Max(200, 110 + (entries.Count * 20)));
            UpdateFrame(window);
            _aboutTimer?.UpdateTimer(2000);
        }

        _aboutTimer?.Remove();
        _aboutTimer = _host.Loop.AddTimer(Refresh);
        window.ShellClosed = () =>
        {
            _aboutTimer?.Remove();
            _aboutTimer = null;
        };
        Refresh();
    }
}
