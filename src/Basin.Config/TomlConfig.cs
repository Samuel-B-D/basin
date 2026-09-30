using Basin.Diagnostics;
using Tomlyn;
using Tomlyn.Model;

namespace Basin.Config;

public static class TomlConfig
{
    public static string DefaultPath(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(configHome) || !Path.IsPathRooted(configHome))
        {
            configHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(configHome, name, name + ".toml");
    }

    public static TomlLoad Load(string? namedPath, string name, bool none, Func<Stream>? template, BasinLogger log)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (none)
        {
            return default;
        }

        var named = namedPath is { Length: > 0 };
        var file = named ? namedPath! : DefaultPath(name);
        if (!named && template is not null && !File.Exists(file))
        {
            using var stream = template();
            Seed(file, stream, log);
        }

        var table = Read(file, out var failure);
        if (table is not null)
        {
            return new TomlLoad(table, file, null);
        }

        if (named)
        {
            return new TomlLoad(null, file, $"{file}: {failure}");
        }

        if (File.Exists(file))
        {
            log.Warn($"{file} did not parse, keeping defaults: {failure}");
        }

        return new TomlLoad(null, file, null);
    }

    public static bool Seed(string file, Stream template, BasinLogger log)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);
        ArgumentNullException.ThrowIfNull(template);
        try
        {
            if (Path.GetDirectoryName(file) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write))
            {
                template.CopyTo(stream);
            }

            log.Info($"wrote the default configuration to {file}");
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            log.Warn($"cannot write {file}, keeping the built-in defaults: {error.Message}");
            return false;
        }
    }

    public static TomlTable? Read(string path, BasinLogger log)
    {
        var table = Read(path, out var failure);
        if (failure is not null && File.Exists(path))
        {
            log.Warn($"{path} did not parse, keeping defaults: {failure}");
        }

        return table;
    }

    public static TomlTable? Read(string path, out string? failure)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string text;
        try
        {
            if (!File.Exists(path))
            {
                failure = "no such file";
                return null;
            }

            text = File.ReadAllText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            failure = error.Message;
            return null;
        }

        try
        {
            failure = null;
            return Toml.ToModel(text);
        }
        catch (TomlException error)
        {
            failure = error.Message;
            return null;
        }
    }

    public static bool Flag(TomlTable table, string key, bool fallback)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.TryGetValue(key, out var value) && value is bool flag ? flag : fallback;
    }

    public static string? Text(TomlTable table, string key)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.TryGetValue(key, out var value) && value is string text && text.Length > 0 ? text : null;
    }

    public static int Number(TomlTable table, string key, int fallback)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.TryGetValue(key, out var value) && value is long number ? (int)number : fallback;
    }

    public static double Number(TomlTable table, string key, double fallback)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return value switch
        {
            double number => number,
            long integer => integer,
            _ => fallback,
        };
    }
}
