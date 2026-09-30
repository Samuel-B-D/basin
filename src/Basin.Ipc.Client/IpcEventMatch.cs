using System.Text.Json;

namespace Basin.Ipc;

public static class IpcEventMatch
{
    public static bool Matches(ReadOnlySpan<byte> data, JsonElement? match)
    {
        if (match is not { } wanted)
        {
            return true;
        }

        var reader = new Utf8JsonReader(data);
        var root = JsonElement.ParseValue(ref reader);
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var key in wanted.EnumerateObject())
        {
            if (!root.TryGetProperty(key.Name, out var value))
            {
                return false;
            }

            if (key.Value.ValueKind == JsonValueKind.String && value.ValueKind == JsonValueKind.String)
            {
                var pattern = key.Value.GetString()!;
                var text = value.GetString()!;
                if (pattern.Length >= 2 && pattern[0] == '*' && pattern[^1] == '*')
                {
                    if (!text.Contains(pattern[1..^1], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
                else if (text != pattern)
                {
                    return false;
                }

                continue;
            }

            if (key.Value.ValueKind == JsonValueKind.String && value.ValueKind == JsonValueKind.Number)
            {
                if (key.Value.GetString() != value.GetRawText())
                {
                    return false;
                }

                continue;
            }

            if (!JsonElement.DeepEquals(key.Value, value))
            {
                return false;
            }
        }

        return true;
    }
}
