using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using System7.Avalonia;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal sealed class DesktopView : Canvas
{
    private readonly List<DesktopIcon> _icons = [];
    private readonly Rectangle _band = new() { Stroke = Brushes.Black, StrokeThickness = 1, StrokeDashArray = [1, 1], IsVisible = false };
    private readonly List<Rectangle> _outlines = [];
    private Point _start;
    private bool _pressed;
    private bool _dragging;
    private bool _banding;
    private DesktopIcon? _pressedIcon;
    private TextBox? _rename;
    private DesktopIcon? _renaming;
    private IDisposable? _renameTimer;

    public DesktopView()
    {
        Background = Brushes.Transparent;
        Children.Add(_band);
        Focusable = true;
    }

    public IReadOnlyList<DesktopIcon> Icons => _icons;

    public System7ColorDepth Depth { get; set; } = System7ColorDepth.Indexed8;

    public event Action<DesktopIcon>? Opened;

    public event Action<IReadOnlyList<DesktopIcon>, DesktopIcon?, Point, bool>? Dropped;

    public event Action<DesktopIcon, string>? Renamed;

    public event Action? Moved;

    public event Action? Pressed;

    public IEnumerable<DesktopIcon> Selection => _icons.Where(i => i.Selected);

    public void SetIcons(IEnumerable<DesktopIcon> icons)
    {
        foreach (var icon in _icons)
        {
            if (icon.Visual is not null)
            {
                Children.Remove(icon.Visual);
            }
        }

        _icons.Clear();
        foreach (var icon in icons)
        {
            _icons.Add(icon);
            Build(icon);
        }
    }

    public void Refresh()
    {
        foreach (var icon in _icons)
        {
            Update(icon);
        }
    }

    public void SelectAll()
    {
        foreach (var icon in _icons)
        {
            icon.Selected = true;
            Update(icon);
        }
    }

    public DesktopIcon? IconAt(Point point) => _icons.LastOrDefault(i => i.PictureBounds.Contains(point) || NameBounds(i).Contains(point));

    private static Rect NameBounds(DesktopIcon icon)
    {
        var width = Math.Max(12, System7Font.Measure(icon.Label) + 4);
        return new Rect(icon.Position.X + 32 - (width / 2), icon.Position.Y + 33, width, 14);
    }

    private void Build(DesktopIcon icon)
    {
        var picture = new Image { Width = 32, Height = 32 };
        var name = new TextBlock { Text = icon.Label, Padding = new Thickness(2, 0, 2, 0) };
        var back = new Border { Child = name, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        var panel = new StackPanel { Spacing = 1, Children = { picture, back } };
        picture.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        icon.Visual = panel;
        icon.Picture = picture;
        icon.Name = name;
        icon.NameBack = back;
        Children.Insert(0, panel);
        Update(icon);
    }

    private void Update(DesktopIcon icon)
    {
        if (icon.Visual is null)
        {
            return;
        }

        var width = Math.Max(64, System7Font.Measure(icon.Label) + 8);
        icon.Visual.Width = width;
        SetLeft(icon.Visual, icon.Position.X + 32 - (width / 2));
        SetTop(icon.Visual, icon.Position.Y);
        icon.Picture!.Source = icon.Family.Image(Depth, icon.Selected);
        icon.Name!.Text = icon.Label;
        icon.Name.Foreground = icon.Selected ? Brushes.White : Brushes.Black;
        icon.NameBack!.Background = icon.Selected ? Brushes.Black : Brushes.White;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Pressed?.Invoke();
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        CommitRename();
        var point = e.GetPosition(this);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _start = point;
        _pressed = true;
        _dragging = false;
        _pressedIcon = IconAt(point);
        if (_pressedIcon is { } icon)
        {
            if (e.ClickCount >= 2)
            {
                _pressed = false;
                Opened?.Invoke(icon);
                return;
            }

            var wasOnlySelection = icon.Selected && _icons.Count(i => i.Selected) == 1;
            if (shift)
            {
                icon.Selected = !icon.Selected;
            }
            else if (!icon.Selected)
            {
                foreach (var other in _icons)
                {
                    other.Selected = ReferenceEquals(other, icon);
                }
            }

            Refresh();
            if (wasOnlySelection && !shift && NameBounds(icon).Contains(point) && icon.Kind is DesktopIconKind.File or DesktopIconKind.Folder or DesktopIconKind.Disk)
            {
                _renameTimer?.Dispose();
                _renameTimer = DispatcherTimer.RunOnce(() => BeginRename(icon), TimeSpan.FromMilliseconds(500));
            }
        }
        else
        {
            if (!shift)
            {
                foreach (var other in _icons)
                {
                    other.Selected = false;
                }

                Refresh();
            }

            _banding = true;
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pressed)
        {
            return;
        }

        var point = e.GetPosition(this);
        var delta = point - _start;
        if (_banding)
        {
            var band = new Rect(_start, point).Normalize();
            _band.IsVisible = true;
            SetLeft(_band, band.X);
            SetTop(_band, band.Y);
            _band.Width = band.Width;
            _band.Height = band.Height;
            foreach (var icon in _icons)
            {
                icon.Selected = band.Intersects(icon.PictureBounds);
            }

            Refresh();
            return;
        }

        if (_pressedIcon is null)
        {
            return;
        }

        if (!_dragging && Math.Abs(delta.X) + Math.Abs(delta.Y) < 4)
        {
            return;
        }

        _renameTimer?.Dispose();
        _dragging = true;
        var selected = Selection.ToList();
        while (_outlines.Count < selected.Count)
        {
            var outline = new Rectangle { Width = 32, Height = 32, Stroke = Brushes.Black, StrokeThickness = 1, StrokeDashArray = [1, 1] };
            _outlines.Add(outline);
            Children.Add(outline);
        }

        for (var i = 0; i < _outlines.Count; i++)
        {
            _outlines[i].IsVisible = i < selected.Count;
            if (i < selected.Count)
            {
                SetLeft(_outlines[i], selected[i].PictureBounds.X + delta.X);
                SetTop(_outlines[i], selected[i].PictureBounds.Y + delta.Y);
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        e.Pointer.Capture(null);
        _band.IsVisible = false;
        foreach (var outline in _outlines)
        {
            outline.IsVisible = false;
        }

        if (_banding)
        {
            _banding = false;
            return;
        }

        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        var point = e.GetPosition(this);
        var selected = Selection.ToList();
        var target = IconAt(point);
        if (target is not null && selected.Contains(target))
        {
            target = null;
        }

        var copy = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (target is null && Bounds.Contains(point) && !OverWindow(point))
        {
            var delta = point - _start;
            foreach (var icon in selected)
            {
                icon.Position = new Point(Math.Max(0, icon.Position.X + delta.X), Math.Max(20, icon.Position.Y + delta.Y));
            }

            Refresh();
            Moved?.Invoke();
            return;
        }

        Dropped?.Invoke(selected, target, point, copy);
    }

    public Func<Point, bool>? OverWindowTest { get; set; }

    private bool OverWindow(Point point) => OverWindowTest?.Invoke(point) == true;

    public void BeginRename(DesktopIcon icon)
    {
        CommitRename();
        _renaming = icon;
        var bounds = NameBounds(icon);
        _rename = CreateRenameBox(icon.Label, Math.Max(80, bounds.Width + 20));
        SetLeft(_rename, icon.Position.X + 32 - (_rename.Width / 2));
        SetTop(_rename, bounds.Y - 1);
        Children.Add(_rename);
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
        _rename.Focus();
        _rename.SelectAll();
    }

    public bool IsRenaming => _rename is not null;

    internal static TextBox CreateRenameBox(string label, double width)
    {
        var box = new TextBox { Text = label, Width = width, Height = 18, Padding = new Thickness(1, 0), AcceptsReturn = false };
        ScrollViewer.SetVerticalScrollBarVisibility(box, Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        ScrollViewer.SetHorizontalScrollBarVisibility(box, Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden);
        System7Theme.SetUseNativeTextEditing(box, true);
        return box;
    }

    private void CommitRename()
    {
        if (_rename is not { } box || _renaming is not { } icon)
        {
            return;
        }

        var text = box.Text?.Trim() ?? "";
        CancelRename();
        if (text.Length > 0 && text != icon.Label && !text.Contains('/'))
        {
            Renamed?.Invoke(icon, text);
        }
    }

    private void CancelRename()
    {
        if (_rename is not null)
        {
            Children.Remove(_rename);
        }

        _rename = null;
        _renaming = null;
    }
}
