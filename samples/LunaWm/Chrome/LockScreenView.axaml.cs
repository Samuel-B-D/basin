using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace LunaWm;

public sealed partial class LockScreenView : UserControl
{
    public static readonly string[] Keys = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "⌫", "0", "↵"];

    private readonly DispatcherTimer _shake = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private double _ringStart = double.NaN;
    private int _shakeFrame;
    private int _shakes;

    public LockScreenView()
    {
        InitializeComponent();
        _shake.Tick += (_, _) => Shake();
        KeyDown += OnViewKey;
        DataContextChanged += (_, _) =>
        {
            if (Model is { } model)
            {
                model.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(LockScreenModel.Shakes) && model.Shakes != _shakes)
                    {
                        _shakes = model.Shakes;
                        _shakeFrame = 0;
                        _shake.Start();
                    }
                    else if (e.PropertyName == nameof(LockScreenModel.PadShown) && model.PadShown && model.Password)
                    {
                        Dispatcher.UIThread.Post(() => PasswordBox.Focus(), DispatcherPriority.Background);
                    }
                };
            }
        };
    }

    private LockScreenModel? Model => DataContext as LockScreenModel;

    private TranslateTransform RingShift => (TranslateTransform)Ring.RenderTransform!;

    private TranslateTransform PadShift => (TranslateTransform)Pad.RenderTransform!;

    private void OnRingPressed(object? sender, PointerPressedEventArgs e)
    {
        _ringStart = e.GetPosition(this).Y;
        e.Pointer.Capture(Ring);
    }

    private void OnRingMoved(object? sender, PointerEventArgs e)
    {
        if (double.IsNaN(_ringStart))
        {
            return;
        }

        var lift = Math.Min(0, e.GetPosition(this).Y - _ringStart);
        RingShift.Y = lift;
        if (lift < -120)
        {
            _ringStart = double.NaN;
            RingShift.Y = 0;
            e.Pointer.Capture(null);
            Model?.RequestPad();
        }
    }

    private void OnRingReleased(object? sender, PointerReleasedEventArgs e)
    {
        _ringStart = double.NaN;
        RingShift.Y = 0;
        e.Pointer.Capture(null);
    }

    private void OnKey(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string key } || Model is not { } model)
        {
            return;
        }

        switch (key)
        {
            case "⌫":
                model.Backspace();
                break;
            case "↵":
                model.Submit();
                break;
            default:
                model.Digit(key);
                break;
        }
    }

    private void OnDone(object? sender, RoutedEventArgs e) => Model?.Submit();

    private void OnPasswordKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Model?.Submit();
        }
    }

    private void OnViewKey(object? sender, KeyEventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }

        if (!model.PadShown)
        {
            model.RequestPad();
            return;
        }

        if (!model.Pin)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            model.Submit();
        }
        else if (e.Key == Key.Back)
        {
            model.Backspace();
        }
        else if (e.KeySymbol is { Length: 1 } symbol && char.IsAsciiDigit(symbol[0]))
        {
            model.Digit(symbol);
        }
    }

    private void Shake()
    {
        _shakeFrame++;
        var t = _shakeFrame / 24.0;
        PadShift.X = t >= 1 ? 0 : Math.Sin(t * Math.PI * 6) * 14 * (1 - t);
        if (t >= 1)
        {
            _shake.Stop();
        }
    }
}
