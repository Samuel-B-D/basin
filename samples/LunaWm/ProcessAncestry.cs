using System.Globalization;

namespace LunaWm;

internal static class ProcessAncestry
{
    public static bool DescendsFrom(int pid, Func<int, bool> isRoot)
    {
        for (var depth = 0; pid > 1 && depth < 32; depth++)
        {
            if (isRoot(pid))
            {
                return true;
            }

            pid = Parent(pid);
        }

        return false;
    }

    public static int Parent(int pid)
    {
        try
        {
            var stat = File.ReadAllText($"/proc/{pid}/stat");
            var close = stat.LastIndexOf(')');
            if (close < 0)
            {
                return 0;
            }

            var fields = stat[(close + 2)..].Split(' ');
            return fields.Length > 1 && int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parent) ? parent : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
