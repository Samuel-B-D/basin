using System.Globalization;
using Basin;

namespace LunaWm;

internal sealed partial class LunaShell
{
    private IEventSource? _clockTimer;
    private string? _clockOverride;

    private void AttachStatusBar(LunaView view)
    {
        var bar = new StatusBar(view.StatusBarFrame, _ui, _chromeIndex);
        view.Bar = bar;
        bar.Model.Pressed += what => StatusBarPressed(view, what);
        bar.Model.Clock = ClockText();
        bar.Fade.Jump(1);
        PlaceStatusBar(view);
        UpdateTitle(view);
        SyncIndicators(view);
    }

    private void PlaceStatusBar(LunaView view)
    {
        view.Bar?.Place(view.Box.Width, view.Scale);
    }

    internal void UpdateTitle(LunaView? view)
    {
        if (view?.Bar is not { } bar)
        {
            return;
        }

        var title = view.InCardView || view.Deck.ActiveCard is null
            ? DateText()
            : TitleOf(view.Deck.ActiveCard);
        if (bar.Model.Title != title)
        {
            bar.Model.Title = title;
        }
    }

    private static string TitleOf(Card card)
    {
        var label = card.Title.Length > 0 ? card.Title : card.AppId;
        return label.Length > 0 ? label : "Untitled";
    }

    private void FadeStatusBar(LunaView view)
    {
        if (view.Bar is not { } bar)
        {
            return;
        }

        var shown = !view.BarHidden || view.BarRevealed;
        if (shown)
        {
            bar.Chrome.Enabled = true;
        }

        bar.Fade.Start(LunaMotion.StatusBarFade, bar.Fade.Value, shown ? 1 : 0, Now, 1);
        view.StatusBarFrame.Alpha = (float)bar.Fade.Value;
        view.Scheduler?.ScheduleRepaint();
        _report.Line($"BAR {(shown ? "shown" : "hidden")}");
    }

    private bool AdvanceStatusBar(LunaView view, long now)
    {
        if (view.Bar is not { } bar || !bar.Fade.Advance(now))
        {
            return false;
        }

        view.StatusBarFrame.Alpha = (float)bar.Fade.Value;
        if (!bar.Fade.IsRunning && bar.Fade.Value <= 0)
        {
            bar.Chrome.Enabled = false;
        }

        return bar.Fade.IsRunning;
    }

    private void StartClock()
    {
        _clockTimer = _host.Loop.AddTimer(OnClockTick);
        OnClockTick();
    }

    private void StopClock()
    {
        _clockTimer?.Remove();
        _clockTimer = null;
    }

    private void OnClockTick()
    {
        var text = ClockText();
        foreach (var view in _views)
        {
            if (view.Bar is { } bar && bar.Model.Clock != text)
            {
                bar.Model.Clock = text;
            }

            UpdateTitle(view);
            ClockChanged(view);
        }

        var now = DateTime.Now;
        var delay = _config.ShowSeconds ? 1000 - now.Millisecond : ((60 - now.Second) * 1000) - now.Millisecond;
        _clockTimer?.UpdateTimer(Math.Max(50, delay));
    }

    private void ApplyClockFormat() => OnClockTick();

    internal void SetClockOverride(string? text)
    {
        _clockOverride = text is { Length: > 0 } ? text : null;
        OnClockTick();
    }

    private string ClockText()
    {
        if (_clockOverride is { } fixedText)
        {
            return fixedText;
        }

        var format = (_config.Clock24, _config.ShowSeconds) switch
        {
            (true, true) => "H:mm:ss",
            (true, false) => "H:mm",
            (false, true) => "h:mm:ss tt",
            _ => "h:mm tt",
        };
        return DateTime.Now.ToString(format, CultureInfo.InvariantCulture);
    }

    private string DateText() =>
        _clockOverride is not null ? "Monday, January 2" : DateTime.Now.ToString("dddd, MMMM d", CultureInfo.InvariantCulture);
}
