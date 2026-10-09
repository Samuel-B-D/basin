namespace LunaWm;

internal static class CardGrouping
{
    public static bool JoinsActive(in CardFacts candidate, in CardFacts? active, bool activeFocused, bool cardView, StackChoice choice)
    {
        if (choice == StackChoice.New || active is not { } focus)
        {
            return false;
        }

        if (choice == StackChoice.Join)
        {
            return true;
        }

        if (candidate.ShellLaunched || cardView || !activeFocused)
        {
            return false;
        }

        var samePid = candidate.Pid > 0 && candidate.Pid == focus.Pid;
        var sameApp = candidate.AppId.Length > 0 && candidate.AppId == focus.AppId;
        return samePid || sameApp;
    }
}
