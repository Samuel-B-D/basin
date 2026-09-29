using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System7.Avalonia;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal sealed class FinderView : DockPanel
{
    private const int Batch = 200;

    private readonly TextBlock _header = new() { Margin = new Thickness(6, 2, 6, 2) };
    private readonly ListBox _list = new() { SelectionMode = SelectionMode.Multiple, Classes = { "windowScroll" } };
    private readonly Func<FinderEntry, System7IconFamily> _icon;
    private List<FinderEntry> _entries = [];
    private readonly Dictionary<string, Dictionary<string, Point>> _placed = new(StringComparer.Ordinal)
    {
        ["icon"] = new(StringComparer.Ordinal),
        ["small"] = new(StringComparer.Ordinal),
    };
    private readonly Canvas _overlay = new();
    private TextBox? _rename;
    private FinderEntry? _renaming;
    private IDisposable? _renameTimer;
    private FinderEntry? _soleSelection;
    private int _generation;
    private Point _start;
    private bool _pressed;
    private bool _dragged;

    public FinderView(string folder, string mode, Func<FinderEntry, System7IconFamily> icon)
    {
        Folder = folder;
        _icon = icon;
        var line = new Border { Height = 1, Background = Brushes.Black };
        SetDock(_header, Dock.Top);
        SetDock(line, Dock.Top);
        Children.Add(_header);
        Children.Add(line);
        Children.Add(new Panel { Children = { _list, _overlay } });
        _list.ContainerPrepared += (_, e) =>
        {
            if (e.Container is ListBoxItem item && item.Tag is FinderEntry entry)
            {
                Decorate(item, entry);
            }
        };
        _list.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _list.AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _list.AddHandler(PointerReleasedEvent, (_, _) =>
        {
            _pressed = false;
            _soleSelection = Selection is [var one] ? one : null;
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        _list.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Mode = mode;
        ApplyMode();
    }

    public string Folder { get; }

    public string Mode { get; private set; }

    public System7ColorDepth Depth { get; set; }

    public event Action<FinderEntry, bool>? Opened;

    public event Action? Arranged;

    public event Action<FinderEntry, string>? Renamed;

    public bool IsSpatial => Mode is "icon" or "small";

    public bool IsRenaming => _rename is not null;

    public double LayoutWidth { get; set; } = 360;

    public Point DragOrigin => _start;

    public IReadOnlyDictionary<string, Point> Positions => PositionsOf(Mode);

    public IReadOnlyDictionary<string, Point> PositionsOf(string mode) =>
        _placed.TryGetValue(mode, out var positions) ? positions : new Dictionary<string, Point>();

    private Dictionary<string, Point> Current => _placed.GetValueOrDefault(Mode) ?? [];

    private (double Width, double Height) Cell => CellOf(Mode);

    private static (double Width, double Height) CellOf(string mode) => mode == "small" ? (128, 20) : (112, 64);

    public event Action<IReadOnlyList<FinderEntry>, bool>? DragStarted;

    public IReadOnlyList<FinderEntry> Selection => _list.SelectedItems?.OfType<ListBoxItem>().Select(i => (FinderEntry)i.Tag!).ToList() ?? [];

    public IReadOnlyList<FinderEntry> Entries => _entries;

    public void SetMode(string mode)
    {
        if (mode == Mode)
        {
            return;
        }

        CommitRename();
        Mode = mode;
        ApplyMode();
        AssignSlots();
        Populate();
    }

    public void SetHeader(string text) => _header.Text = text;

    public void SetPositions(string mode, IEnumerable<KeyValuePair<string, Point>> positions)
    {
        if (!_placed.TryGetValue(mode, out var placed))
        {
            return;
        }

        placed.Clear();
        foreach (var (name, point) in positions)
        {
            placed[name] = point;
        }
    }

    public Rect? ItemRect(FinderEntry entry)
    {
        if (_list.GetRealizedContainers().OfType<ListBoxItem>().FirstOrDefault(i => ReferenceEquals(i.Tag, entry)) is not { } item)
        {
            return null;
        }

        Visual target = item.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "Icon" && c.Bounds.Width > 0) ?? (Visual)item;
        return target.TranslatePoint(default, this) is { } origin ? new Rect(origin, target.Bounds.Size) : null;
    }

    public Point PanelPoint(Point local) =>
        _list.ItemsPanelRoot is { } panel && this.TranslatePoint(local, panel) is { } point ? point : local;

    public IReadOnlyList<(string Name, Vector Offset)> DragGrab()
    {
        var origin = PanelPoint(_start);
        return [.. Selection.Where(e => Current.ContainsKey(e.Name)).Select(e => (e.Name, Current[e.Name] - origin))];
    }

    public void MoveSelection(Vector delta)
    {
        var positions = Current;
        foreach (var entry in Selection)
        {
            if (positions.TryGetValue(entry.Name, out var point))
            {
                positions[entry.Name] = new Point(Math.Max(0, point.X + delta.X), Math.Max(0, point.Y + delta.Y));
            }
        }

        Place();
        Arranged?.Invoke();
    }

    public void PlaceNames(IEnumerable<(string Name, Point At)> names)
    {
        if (!IsSpatial)
        {
            return;
        }

        foreach (var (name, at) in names)
        {
            Current[name] = new Point(Math.Max(0, at.X), Math.Max(0, at.Y));
        }

        Place();
        Arranged?.Invoke();
    }

    public void RenamePosition(string from, string to)
    {
        foreach (var positions in _placed.Values)
        {
            if (positions.Remove(from, out var point))
            {
                positions[to] = point;
            }
        }
    }

    public void CleanUp()
    {
        var positions = Current;
        var (width, height) = Cell;
        var taken = new HashSet<(int, int)>();
        foreach (var entry in _entries.OrderBy(e => positions.GetValueOrDefault(e.Name).Y).ThenBy(e => positions.GetValueOrDefault(e.Name).X))
        {
            var point = positions.GetValueOrDefault(entry.Name);
            var cell = Nearest(taken, (int)Math.Round(point.X / width), (int)Math.Round(point.Y / height), width, height);
            taken.Add(cell);
            positions[entry.Name] = new Point(cell.Item1 * width, cell.Item2 * height);
        }

        Place();
        Arranged?.Invoke();
    }

    public void BeginRename(FinderEntry entry)
    {
        CommitRename();
        if (_list.GetRealizedContainers().OfType<ListBoxItem>().FirstOrDefault(i => ReferenceEquals(i.Tag, entry)) is not { } item ||
            NameRect(item, entry) is not { } rect)
        {
            return;
        }

        _renaming = entry;
        _rename = DesktopView.CreateRenameBox(entry.Name, Math.Max(80, rect.Width + 20));
        Canvas.SetLeft(_rename, Mode == "icon" ? rect.Center.X - (_rename.Width / 2) : rect.X - 2);
        Canvas.SetTop(_rename, rect.Y - 1);
        _overlay.Children.Add(_rename);
        _rename.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Return)
            {
                CommitRename();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelRename();
                e.Handled = true;
            }
        };
        var box = _rename;
        box.Focus();
        box.SelectAll();
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_rename, box))
            {
                box.Focus();
                box.SelectAll();
                box.LostFocus += (_, _) => CommitRename();
            }
        }, DispatcherPriority.Loaded);
    }

    public void CommitRename()
    {
        if (_rename is not { } box || _renaming is not { } entry)
        {
            return;
        }

        var text = box.Text?.Trim() ?? "";
        CancelRename();
        if (text.Length > 0 && text != entry.Name && !text.Contains('/'))
        {
            Renamed?.Invoke(entry, text);
        }
    }

    private void CancelRename()
    {
        _renameTimer?.Dispose();
        _renameTimer = null;
        var box = _rename;
        _rename = null;
        _renaming = null;
        if (box is not null)
        {
            _overlay.Children.Remove(box);
        }
    }

    private Rect? NameRect(ListBoxItem item, FinderEntry entry)
    {
        Visual? label = Mode switch
        {
            "icon" or "small" => item.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "Band"),
            _ => item.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == entry.Name),
        };
        return label is not null && label.TranslatePoint(default, _overlay) is { } origin ? new Rect(origin, label.Bounds.Size) : null;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_rename is null && e.Key is Key.Enter or Key.Return && e.KeyModifiers == KeyModifiers.None && Selection is [var only])
        {
            BeginRename(only);
            e.Handled = true;
        }
    }

    private static (int, int) Nearest(HashSet<(int, int)> taken, int column, int row, double cellWidth, double cellHeight)
    {
        for (var radius = 0; ; radius++)
        {
            (int, int)? best = null;
            var bestDistance = double.MaxValue;
            for (var r = Math.Max(0, row - radius); r <= row + radius; r++)
            {
                for (var c = Math.Max(0, column - radius); c <= column + radius; c++)
                {
                    if (Math.Max(Math.Abs(r - row), Math.Abs(c - column)) != radius || taken.Contains((c, r)))
                    {
                        continue;
                    }

                    var distance = Math.Pow((c - column) * cellWidth, 2) + Math.Pow((r - row) * cellHeight, 2);
                    if (distance < bestDistance)
                    {
                        best = (c, r);
                        bestDistance = distance;
                    }
                }
            }

            if (best is { } found)
            {
                return found;
            }
        }
    }

    private void AssignSlots()
    {
        var names = _entries.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var placed in _placed.Values)
        {
            foreach (var name in placed.Keys.Where(n => !names.Contains(n)).ToList())
            {
                placed.Remove(name);
            }
        }

        if (!IsSpatial)
        {
            return;
        }

        var positions = Current;
        var (cellWidth, cellHeight) = Cell;
        var width = _list.Bounds.Width > cellWidth ? _list.Bounds.Width - 16 : LayoutWidth - 16;
        var columns = Math.Max(1, (int)(width / cellWidth));
        var taken = positions.Values.Select(p => ((int)Math.Round(p.X / cellWidth), (int)Math.Round(p.Y / cellHeight))).ToHashSet();
        var slot = 0;
        foreach (var entry in _entries)
        {
            if (positions.ContainsKey(entry.Name))
            {
                continue;
            }

            while (taken.Contains((slot % columns, slot / columns)))
            {
                slot++;
            }

            taken.Add((slot % columns, slot / columns));
            positions[entry.Name] = new Point(slot % columns * cellWidth, slot / columns * cellHeight);
        }
    }

    private void Place()
    {
        if (!IsSpatial)
        {
            return;
        }

        foreach (var container in _list.GetRealizedContainers())
        {
            if (container is ListBoxItem { Tag: FinderEntry entry } item)
            {
                PlaceItem(item, entry);
            }
        }

        _list.ItemsPanelRoot?.InvalidateMeasure();
    }

    private void PlaceItem(ListBoxItem item, FinderEntry entry)
    {
        var point = Current.GetValueOrDefault(entry.Name);
        if (Mode == "small")
        {
            item.Width = Cell.Width;
        }

        Canvas.SetLeft(item, point.X);
        Canvas.SetTop(item, point.Y);
    }

    public void SetEntries(IEnumerable<FinderEntry> entries)
    {
        _entries = Mode == "name"
            ? [.. entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)]
            : [.. entries.OrderBy(e => e.IsDirectory ? 0 : 1).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)];
        AssignSlots();
        Populate();
    }

    public void SelectAll() => _list.SelectAll();

    public FinderEntry? FolderAt(Point point)
    {
        if (_list.InputHitTest(this.TranslatePoint(point, _list) ?? point) is Visual hit &&
            hit.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { Tag: FinderEntry { IsDirectory: true } entry })
        {
            return entry;
        }

        return null;
    }

    private void ApplyMode()
    {
        if (IsSpatial)
        {
            _list.ItemsPanel = new FuncTemplate<Panel?>(() => new FinderIconPanel());
            ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Auto);
        }
        else
        {
            _list.ClearValue(ItemsControl.ItemsPanelProperty);
            _list.ClearValue(ScrollViewer.HorizontalScrollBarVisibilityProperty);
        }

        _list[!ThemeProperty] = new DynamicResourceExtension(Mode switch
        {
            "icon" => "System7IconListBoxTheme",
            "name" => "System7ListBoxTheme",
            _ => "System7FinderSmallIconListBoxTheme",
        });
    }

    private void Populate()
    {
        var generation = ++_generation;
        _list.Items.Clear();
        var index = 0;
        void Next()
        {
            if (generation != _generation)
            {
                return;
            }

            var end = Math.Min(_entries.Count, index + Batch);
            for (; index < end; index++)
            {
                var entry = _entries[index];
                _list.Items.Add(new ListBoxItem { Tag = entry, Content = Mode == "name" ? Row(entry) : entry.Name });
            }

            if (index < _entries.Count)
            {
                Dispatcher.UIThread.Post(Next, DispatcherPriority.Background);
            }
        }

        Next();
    }

    private static Control Row(FinderEntry entry)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("20,160,70,120,*") };
        var icon = new Avalonia.Controls.Shapes.Path { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var name = new TextBlock { Text = entry.Name };
        var size = new TextBlock { Text = entry.IsDirectory ? "—" : FormatSize(entry.Size), TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 0, 10, 0) };
        var kind = new TextBlock { Text = entry.Kind };
        var date = new TextBlock { Text = entry.Modified.ToString("ddd, MMM d, yyyy", System.Globalization.CultureInfo.CurrentCulture) };
        SetColumn(name, 1);
        SetColumn(size, 2);
        SetColumn(kind, 3);
        SetColumn(date, 4);
        grid.Children.Add(icon);
        grid.Children.Add(name);
        grid.Children.Add(size);
        grid.Children.Add(kind);
        grid.Children.Add(date);
        return grid;
    }

    private static void SetColumn(Control control, int column) => Grid.SetColumn(control, column);

    public static string FormatSize(long bytes) => bytes < 1024 ? $"{bytes}" : $"{(bytes + 1023) / 1024:N0}K";

    private void Decorate(ListBoxItem item, FinderEntry entry)
    {
        var family = _icon(entry);
        if (Mode == "icon")
        {
            System7Theme.SetListIcon(item, family);
            PlaceItem(item, entry);
        }
        else if (Mode == "name")
        {
            if (item.Content is Grid { Children: [Avalonia.Controls.Shapes.Path icon, ..] })
            {
                icon.Data = (family.Small ?? family).Monochrome.InkGeometry;
                icon.Fill = item.Foreground;
                item.GetObservable(TemplatedControl.ForegroundProperty).Subscribe(new Avalonia.Reactive.AnonymousObserver<IBrush?>(brush => icon.Fill = brush));
            }
        }
        else
        {
            System7Theme.SetListIcon(item, family.Small ?? family);
            PlaceItem(item, entry);
        }
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
        _dragged = false;
        _start = e.GetPosition(this);
        CommitRename();
        var item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        var sole = _soleSelection;
        _soleSelection = null;
        if (_pressed && e.ClickCount >= 2 && item is { Tag: FinderEntry entry })
        {
            _renameTimer?.Dispose();
            _renameTimer = null;
            _pressed = false;
            Opened?.Invoke(entry, e.KeyModifiers.HasFlag(KeyModifiers.Alt));
            return;
        }

        if (_pressed && e.ClickCount == 1 && e.KeyModifiers == KeyModifiers.None && item is { Tag: FinderEntry only } &&
            ReferenceEquals(only, sole) && NameRect(item, only) is { } name && name.Contains(e.GetPosition(_overlay)))
        {
            _renameTimer = DispatcherTimer.RunOnce(() =>
            {
                _renameTimer = null;
                if (!_dragged && Selection is [var still] && ReferenceEquals(still, only))
                {
                    BeginRename(only);
                }
            }, TimeSpan.FromMilliseconds(500));
        }
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (!_pressed || _dragged)
        {
            return;
        }

        var delta = e.GetPosition(this) - _start;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) < 5 || Selection.Count == 0)
        {
            return;
        }

        _dragged = true;
        _pressed = false;
        _renameTimer?.Dispose();
        _renameTimer = null;
        DragStarted?.Invoke(Selection, e.KeyModifiers.HasFlag(KeyModifiers.Alt));
    }
}
