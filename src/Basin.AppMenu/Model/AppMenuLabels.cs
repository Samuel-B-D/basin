namespace Basin.AppMenu;

public static class AppMenuLabels
{
    public static string StripMnemonic(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (!label.Contains('_', StringComparison.Ordinal))
        {
            return label;
        }

        var result = new System.Text.StringBuilder(label.Length);
        for (var i = 0; i < label.Length; i++)
        {
            if (label[i] != '_')
            {
                result.Append(label[i]);
                continue;
            }

            if (i + 1 < label.Length && label[i + 1] == '_')
            {
                result.Append('_');
                i++;
            }
        }

        return result.ToString();
    }
}
