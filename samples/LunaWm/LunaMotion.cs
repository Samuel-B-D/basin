namespace LunaWm;

internal static class LunaMotion
{
    public static readonly MotionSpec CardLaunch = new("cardLaunch", 400, QtCurve.OutInBounce);
    public static readonly MotionSpec CardSlide = new("cardSlide", 300, QtCurve.OutQuart);
    public static readonly MotionSpec CardMaximize = new("cardMaximize", 300, QtCurve.OutQuart);
    public static readonly MotionSpec CardMinimize = new("cardMinimize", 350, QtCurve.OutCubic);
    public static readonly MotionSpec CardDelete = new("cardDelete", 300, QtCurve.OutCubic);
    public static readonly MotionSpec CardScootAwayOnLaunch = new("cardScootAwayOnLaunch", 400, QtCurve.OutElastic);
    public static readonly MotionSpec CardShuffleReorder = new("cardShuffleReorder", 350, QtCurve.OutCubic);
    public static readonly MotionSpec CardGroupReorder = new("cardGroupReorder", 500, QtCurve.OutCubic);
    public static readonly MotionSpec CardSwitchReachEndMaximized = new("cardSwitchReachEndMaximized", 200, QtCurve.OutCubic);
    public static readonly MotionSpec CardSwitchMaximized = new("cardSwitchMaximized", 350, QtCurve.OutCubic);
    public static readonly MotionSpec CardLoadingPulse = new("cardLoadingPulse", 1000, QtCurve.InQuad, PauseMs: 1000);
    public static readonly MotionSpec CardGhost = new("cardGhost", 750, QtCurve.OutQuart);
    public static readonly MotionSpec CardDimming = new("cardDimming", 300, QtCurve.OutCubic);
    public static readonly MotionSpec DashboardSnap = new("dashboardSnap", 500, QtCurve.OutCubic);
    public static readonly MotionSpec QuickLaunch = new("quickLaunch", 350, QtCurve.OutCubic);
    public static readonly MotionSpec Launcher = new("launcher", 350, QtCurve.InOutQuint);
    public static readonly MotionSpec UniversalSearchCrossFade = new("universalSearchCrossFade", 150, QtCurve.OutCubic);
    public static readonly MotionSpec LockWindowFade = new("lockWindowFade", 150, QtCurve.InQuad);
    public static readonly MotionSpec LockPin = new("lockPin", 250, QtCurve.InOutQuint);
    public static readonly MotionSpec LockFade = new("lockFade", 200, QtCurve.Linear);
    public static readonly MotionSpec StatusBarFade = new("statusBarFade", 300, QtCurve.Linear);
    public static readonly MotionSpec StatusBarTitleChange = new("statusBarTitleChange", 300, QtCurve.Linear);
    public static readonly MotionSpec PositiveSpace = new("positiveSpaceChange", 400, QtCurve.OutCubic);
    public static readonly MotionSpec StatusBarMenuFade = new("statusBarMenuFade", 200, QtCurve.Linear);

    public static readonly MotionSpec[] All =
    [
        CardLaunch, CardSlide, CardMaximize, CardMinimize, CardDelete, CardScootAwayOnLaunch,
        CardShuffleReorder, CardGroupReorder, CardSwitchReachEndMaximized, CardSwitchMaximized,
        CardLoadingPulse, CardGhost, CardDimming, DashboardSnap, QuickLaunch, Launcher,
        UniversalSearchCrossFade, LockWindowFade, LockPin, LockFade, StatusBarFade,
        StatusBarTitleChange, StatusBarMenuFade, PositiveSpace,
    ];
}
