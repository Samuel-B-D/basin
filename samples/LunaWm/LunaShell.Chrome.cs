namespace LunaWm;

internal sealed partial class LunaShell
{
    private void AttachChrome(LunaView view)
    {
        AttachLauncher(view);
        AttachDashboard(view);
        AttachLock(view);
        AttachJustType(view);
        AttachKeyboardSurface(view);
    }

    private void PlaceChrome(LunaView view)
    {
        if (view.Menu is { Open: true } menu)
        {
            PlaceMenu(view, menu);
        }

        PlaceLauncher(view);
        PlaceDashboard(view);
        PlaceLock(view);
        PlaceJustType(view);
        if (ReferenceEquals(view, _kbView))
        {
            PlaceKeyboard(view, immediate: true);
        }
    }

    private bool AdvanceChrome(LunaView view, long now)
    {
        var animating = AdvanceStatusBar(view, now);
        animating |= AdvanceMenu(view, now);
        animating |= AdvanceLauncher(view, now);
        animating |= AdvanceNotifications(view, now);
        animating |= AdvanceLock(view, now);
        animating |= AdvanceJustType(view, now);
        animating |= AdvanceKeyboard(view, now);
        return animating;
    }

    private void CloseShellUi(LunaView view) => CloseOtherShellUi(view, keepMenu: false);

    private void CloseOtherShellUi(LunaView view, bool keepMenu)
    {
        if (!keepMenu)
        {
            CloseMenu(view);
        }

        ShowDashboard(view, false);

        ShowLauncher(view, false);
        CloseJustType(view);
        if (_dockRequested)
        {
            ShowQuickLaunch(view, false);
        }
    }

    private bool CloseTopShellUi(LunaView view)
    {
        if (view.Menu is { Open: true, Kind: "app" })
        {
            CloseMenu(view);
            return true;
        }

        if (DashboardOpen(view))
        {
            ShowDashboard(view, false);
            return true;
        }

        if (view.Menu is { Open: true })
        {
            CloseMenu(view);
            return true;
        }

        if (LauncherOpen(view))
        {
            ShowLauncher(view, false);
            return true;
        }

        if (JustTypeOpen(view))
        {
            CloseJustType(view);
            return true;
        }

        if (QuickLaunchOpen(view))
        {
            ShowQuickLaunch(view, false);
            return true;
        }

        return false;
    }

    private bool DismissOnPress(LunaView view, double x, double y)
    {
        if (QuickLaunchOpen(view) && y < view.Box.Height - QuickLaunchModel.Height)
        {
            ShowQuickLaunch(view, false);
            return true;
        }

        return false;
    }

    private void SettleChrome(LunaView view)
    {
        SettleMenu(view);
        SettleLauncher(view);
        SettleNotifications(view);
        SettleLock(view);
        SettleJustType(view);
        SettleKeyboard(view);
        if (view.Bar is { } bar && bar.Fade.IsRunning)
        {
            bar.Fade.Settle();
            view.StatusBarFrame.Alpha = (float)bar.Fade.Value;
        }
    }

    private void PrintChromeState(LunaView view)
    {
        _report.Line($"  indicators {IndicatorState}");
        PrintMenuState(view);
        PrintLauncherState(view);
        PrintNotificationState(view);
        _report.Line($"  justtype {(JustTypeOpen(view) ? "open" : "closed")} query={view.JustType?.Model.Query}");
        PrintKeyboardState();
        _report.Line($"  lock {(view.LockUi is { } lockUi && lockUi.Chrome.Enabled ? (lockUi.Model.PadShown ? "pad" : "shown") : "hidden")}");
    }

}
