using System.Globalization;

namespace Basin.Audio.PipeWire;

public sealed class AudioProcessTable : IAudioProcessTable
{
    public static AudioProcessTable Instance { get; } = new();

    public int ParentOf(int pid) => StatField(pid, 1);

    public int GroupOf(int pid) => StatField(pid, 2);

    public string? FlatpakAppOf(int pid)
    {
        if (pid <= 0)
        {
            return null;
        }

        string text;
        try
        {
            text = File.ReadAllText($"/proc/{pid}/root/.flatpak-info");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var inApplication = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inApplication = line == "[Application]";
                continue;
            }

            if (inApplication && line.StartsWith("name=", StringComparison.Ordinal))
            {
                return line["name=".Length..].Trim();
            }
        }

        return null;
    }

    private static int StatField(int pid, int index)
    {
        if (pid <= 0)
        {
            return 0;
        }

        string stat;
        try
        {
            stat = File.ReadAllText($"/proc/{pid}/stat");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return 0;
        }

        var close = stat.LastIndexOf(')');
        if (close < 0)
        {
            return 0;
        }

        var fields = stat[(close + 2)..].Split(' ');
        return fields.Length > index && int.TryParse(fields[index], NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
