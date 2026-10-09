using Basin;
using Basin.Scene;

namespace LunaWm;

internal sealed partial class LunaShell
{
    private long Now => Environment.TickCount64;

    private double TimeScale => _config.TimeScale;

    private CardMetrics MetricsFor(LunaView view) => new(
        view.Box.Width,
        view.Box.Height,
        LunaView.StatusBarHeight,
        _config.ActiveRatio,
        _config.InactiveRatio,
        _config.RotationFactor,
        _config.StackGap,
        _config.Dim,
        view.InCardView ? 0 : view.CardInset);

    internal void LayoutCards(LunaView view, bool animate, MotionSpec? spec = null)
    {
        view.Metrics = MetricsFor(view);
        var deck = view.Deck;
        if (deck.IsEmpty)
        {
            view.CardsSettled = true;
            UpdateTitle(view);
            SyncDock(view, animate);
            return;
        }

        if (view.InCardView)
        {
            deck.LayoutCardView(view.Metrics, view.Scroll);
        }
        else
        {
            deck.LayoutMaximized(view.Metrics, deck.ActiveCard?.Fullscreen == true);
        }

        var now = Now;
        var motion = spec ?? LunaMotion.CardSlide;
        foreach (var stack in deck)
        {
            foreach (var card in stack)
            {
                if (card.Closing && card.Animating)
                {
                    continue;
                }

                card.Slot.Enabled = true;
                if (animate)
                {
                    card.AnimateTo(card.Target, motion, now, TimeScale);
                }
                else
                {
                    card.Jump(card.Target);
                }
            }
        }

        Restack(view);
        foreach (var card in _dying)
        {
            if (ReferenceEquals(card.Home, view) && !card.Slot.IsDestroyed)
            {
                card.Slot.RaiseToTop();
            }
        }

        view.CardsSettled = false;
        if (!animate)
        {
            SettleCards(view);
        }

        UpdateTitle(view);
        SyncDock(view, animate);
        view.Scheduler?.ScheduleRepaint();
    }

    private static void Restack(LunaView view)
    {
        foreach (var stack in view.Deck)
        {
            foreach (var card in stack)
            {
                card.Slot.RaiseToTop();
            }
        }

        if (view.Deck.ActiveCard is { } active)
        {
            if (!view.InCardView)
            {
                active.Slot.RaiseToTop();
            }
            else if (view.Deck.ActiveStack is { } activeStack)
            {
                foreach (var card in activeStack)
                {
                    card.Slot.RaiseToTop();
                }
            }
        }

        view.Ghost?.Parent?.RaiseToTop();
    }

    private void SettleCards(LunaView view)
    {
        view.CardsSettled = true;
        if (view.InCardView || view.Deck.ActiveCard is not { } active)
        {
            return;
        }

        foreach (var stack in view.Deck)
        {
            foreach (var card in stack)
            {
                if (!ReferenceEquals(card, active) && !card.Animating)
                {
                    card.Slot.Enabled = false;
                }
            }
        }

        AnnounceScales();
    }

    private void Advance(LunaView view)
    {
        var now = Now;
        var animating = false;
        foreach (var stack in view.Deck)
        {
            foreach (var card in stack)
            {
                if (card.Advance(now))
                {
                    animating |= card.Animating;
                }
            }
        }

        for (var i = _dying.Count - 1; i >= 0; i--)
        {
            var card = _dying[i];
            if (!ReferenceEquals(card.Home, view))
            {
                continue;
            }

            if (card.Advance(now))
            {
                animating |= card.Animating;
            }

            if (!card.Animating)
            {
                _dying.RemoveAt(i);
                EndFlight(card);
            }
        }

        animating |= AdvanceGhost(view, now);
        animating |= AdvanceChrome(view, now);
        if (animating)
        {
            view.Scheduler?.ScheduleRepaint();
        }
        else if (!view.CardsSettled && !AnyDragOn(view))
        {
            SettleCards(view);
        }
    }

    private readonly List<Card> _dying = [];

    internal void EnterCardView(LunaView view)
    {
        if (view.InCardView || view.Deck.IsEmpty)
        {
            return;
        }

        view.InCardView = true;
        view.Scroll = 0;
        if (Focused is { } focused && ReferenceEquals((focused.Parent ?? focused).Home, view))
        {
            focused.Handle.SetActivated(false);
            Seat.Keyboard.NotifyClearFocus();
        }

        SyncBarVisibility(view);
        LayoutCards(view, animate: true, spec: LunaMotion.CardMinimize);
        _report.Line("CARDVIEW on");
    }

    internal void Maximize(LunaView view)
    {
        if (view.Deck.ActiveCard is not { } card)
        {
            return;
        }

        CloseShellUi(view);
        var wasCardView = view.InCardView;
        view.InCardView = false;
        view.Scroll = 0;
        SyncBarVisibility(view);
        LayoutCards(view, animate: true, spec: LunaMotion.CardMaximize);
        Focus(card);
        if (wasCardView)
        {
            _report.Line($"CARDVIEW off {card.Id}");
        }
    }

    internal void ToggleCardView(LunaView view)
    {
        if (view.InCardView)
        {
            Maximize(view);
        }
        else
        {
            EnterCardView(view);
        }
    }

    internal void SwitchCard(LunaView view, int direction)
    {
        if (view.InCardView || view.Deck.ActiveCard is not { } active)
        {
            return;
        }

        if (view.Deck.Neighbor(active, direction) is not { } next)
        {
            var bump = view.Metrics.Maximized with { X = view.Metrics.Maximized.X - (direction * view.Metrics.Width * 0.05) };
            active.AnimateTo(bump, LunaMotion.CardSwitchReachEndMaximized, Now, TimeScale);
            active.Target = view.Metrics.Maximized;
            _bounce = active;
            view.Scheduler?.ScheduleRepaint();
            _report.Line($"SWITCH end {active.Id}");
            return;
        }

        view.Deck.Activate(next);
        LayoutCards(view, animate: true, spec: LunaMotion.CardSwitchMaximized);
        Focus(next);
        _report.Line($"SWITCH {next.Id}");
    }

    private Card? _bounce;

    private void FlickClose(LunaView view, Card card, double velocityY)
    {
        var pose = card.Current;
        var gone = pose with { Y = -(view.Metrics.CardHeight * pose.Scale), Brightness = pose.Brightness };
        card.AnimateTo(gone, LunaMotion.CardDelete, Now, TimeScale);
        card.Alpha = 1;
        card.BeginFlight();
        view.Deck.Remove(card);
        _dying.Add(card);
        CloseCard(card);
        LayoutCards(view, animate: true, spec: LunaMotion.CardSlide);
        if (view.Deck.IsEmpty)
        {
            UpdateTitle(view);
        }

        _report.Line($"FLICK {card.Id} {velocityY:0}");
    }

    private void EndFlight(Card card)
    {
        if (!card.EndFlight() && !card.Slot.IsDestroyed)
        {
            card.Slot.Reparent(_limbo);
        }

        _report.Line($"FLOWN {card.Id}");
    }

    private void EndAllFlights()
    {
        foreach (var card in _dying)
        {
            card.EndFlight();
        }

        _dying.Clear();
    }

    internal const int GhostIconSize = 128;

    internal static RenderColor GhostColor(uint average)
    {
        if (average == 0)
        {
            return LunaColors.Ghost;
        }

        var ghost = LunaColors.Ghost;
        float Mix(uint channel, float gray) => (((channel & 0xff) / 255f) * 0.45f) + (gray * 0.55f);
        return new RenderColor(Mix(average >> 16, ghost.R), Mix(average >> 8, ghost.G), Mix(average, ghost.B), 1f);
    }

    private void ShowGhost(LunaView view, string label, string? iconName = null)
    {
        ClearGhost(view);
        var metrics = MetricsFor(view);
        var tree = new SceneTree(view.Cards);
        var frame = new SceneTransform(tree);
        var width = (int)metrics.CardWidth;
        var height = (int)metrics.CardHeight;
        var shadow = new CardShadow(frame, _shadowTile);
        shadow.Place(width, height);
        var icon = _icons.Raster(iconName, (int)Math.Ceiling(GhostIconSize * view.Scale), out var average);
        var color = GhostColor(average);
        var fill = new SceneRect(frame, width, height, color);
        if (icon is not null)
        {
            var node = new SceneBuffer(frame) { InputEnabled = false };
            node.SetBuffer(icon);
            node.DestinationWidth = GhostIconSize;
            node.DestinationHeight = GhostIconSize;
            node.SetPosition((width - GhostIconSize) / 2, (height - GhostIconSize) / 2);
        }

        view.GhostIcon = icon;
        view.Ghost = frame;
        view.GhostFill = fill;
        view.GhostShadow = shadow;
        view.GhostStarted = Now;
        var pose = metrics.Maximized;
        frame.Matrix = RenderTransform.Multiply(
            RenderTransform.Translation(pose.X, pose.Y),
            RenderTransform.Translation(-width / 2.0, -height / 2.0));
        view.GhostPulse.Start(LunaMotion.CardGhost, 0, 1, Now, TimeScale);
        view.Scheduler?.ScheduleRepaint();
        _report.Line($"GHOST {label}{(icon is null ? "" : $" icon={icon.Width} color=#{(uint)(color.R * 255):x2}{(uint)(color.G * 255):x2}{(uint)(color.B * 255):x2}")}");
    }

    private void ClearGhost(LunaView view)
    {
        if (view.Ghost is { } ghost)
        {
            ghost.Parent?.Destroy();
            view.GhostIcon?.Destroy();
            view.GhostIcon = null;
            view.Ghost = null;
            view.GhostFill = null;
            view.GhostShadow = null;
        }
    }

    private bool AdvanceGhost(LunaView view, long now)
    {
        if (view.Ghost is not { } ghost)
        {
            return false;
        }

        if (now - view.GhostStarted > _config.CloseTimeoutMs && _config.CloseTimeoutMs > 0)
        {
            ClearGhost(view);
            return false;
        }

        if (view.GhostPulse.Advance(now))
        {
            ghost.Alpha = (float)view.GhostPulse.Value;
            return true;
        }

        var cycle = LunaMotion.CardLoadingPulse.DurationMs + LunaMotion.CardLoadingPulse.PauseMs;
        var phase = (now - view.GhostStarted - LunaMotion.CardGhost.DurationMs) % (cycle * 2);
        var t = phase < cycle ? phase / (double)cycle : 2 - (phase / (double)cycle);
        ghost.Alpha = (float)(0.6 + (0.4 * (1 - QtEasing.Evaluate(QtCurve.InQuad, t))));
        return true;
    }

    private void TitleChanged(Card card)
    {
        if ((card.Parent ?? card).Home is { } view)
        {
            UpdateTitle(view);
        }
    }

    private void SyncBarVisibility(LunaView view)
    {
        var hidden = !view.InCardView && view.Deck.ActiveCard is { Fullscreen: true };
        if (hidden != view.BarHidden)
        {
            view.BarHidden = hidden;
            view.BarRevealed = false;
            FadeStatusBar(view);
        }
    }

    private void CancelInteractionsFor(Card card)
    {
        if (_drag is { } drag && ReferenceEquals(drag.Card, card))
        {
            CancelDrag();
        }

        if (ReferenceEquals(_bounce, card))
        {
            _bounce = null;
        }

        if (ReferenceEquals(_pinchCard, card))
        {
            _pinchCard = null;
            _pinchView = null;
        }
    }
}
