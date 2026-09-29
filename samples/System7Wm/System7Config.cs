using Basin.Config;
using Basin.Diagnostics;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal sealed class System7Config
{
    public static readonly string[] RestartOnly = ["keyboard.command"];

    public string? Path { get; private set; }

    public string MainOutput { get; private set; } = "";

    public void SetMainOutput(string name) => MainOutput = name;

    public System7ColorDepth Depth { get; set; } = System7ColorDepth.Indexed8;

    public bool DepthApplications { get; set; }

    public bool DepthDither { get; set; } = true;

    public bool Clock { get; private set; }

    public string ClockFormat { get; private set; } = "h:mm tt";

    public Modifiers Command { get; private set; } = Modifiers.Mod4;

    public int RepeatRate { get; set; } = 25;

    public int RepeatDelay { get; set; } = 600;

    public bool AppLayering { get; private set; } = true;

    public bool ClickThrough { get; private set; }

    public bool XWayland { get; private set; } = true;

    public bool ShadeEnabled { get; set; } = true;

    public int ShadeClicks { get; set; } = 2;

    public string ShadeModifier { get; set; } = "none";

    public bool ShadeSound { get; set; }

    public string Pattern { get; set; } = "Desktop";

    public string Wallpaper { get; private set; } = "";

    public string DiskName { get; private set; } = "";

    public string DiskLabel => DiskName.Length > 0 ? DiskName : DiskRoot;

    public void SetDiskName(string name) => DiskName = name;

    public string DiskRoot { get; private set; } = MountPoints.Containing(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public int DoubleClickMs { get; set; } = 500;

    public double? Tracking { get; set; }

    public string AlertSound { get; private set; } = "";

    public void SetAlertSound(string path) => AlertSound = path;

    public int Volume { get; set; } = -1;

    public string TerminalCommand { get; private set; } = "";

    public string TerminalShell { get; private set; } = "";

    public int TerminalFontSize { get; private set; } = 9;

    public int TerminalScrollback { get; private set; } = 1000;

    public Hotkey? NewTerminal { get; private set; } = new(Keysym.FromName("t"), Modifiers.Super | Modifiers.Alt, "new_terminal", null);

    public List<System7Rule> Rules { get; } = [];

    public static string DefaultPath() => TomlConfig.DefaultPath("system7-wm");

    public static string Template()
    {
        using var stream = typeof(System7Config).Assembly.GetManifestResourceStream("system7-wm.toml")
            ?? throw new InvalidOperationException("system7-wm.toml is not embedded");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static System7Config Load(string? path, bool none, BasinLogger log)
    {
        var config = new System7Config();
        if (none)
        {
            return config;
        }

        var named = path is { Length: > 0 };
        var file = named ? path! : DefaultPath();
        if (!named && !File.Exists(file))
        {
            Seed(file, log);
        }

        config.Path = file;
        if (TomlConfig.Read(file, log) is not { } table)
        {
            return config;
        }

        var reader = new TomlReader(table, log);
        if (reader.Section("display") is { } display)
        {
            config.MainOutput = display.Text("main") ?? "";
            var depth = display.Text("depth");
            config.Depth = depth is null ? config.Depth : ColorDepths.Parse(depth) ?? Warn(log, $"[display] depth '{depth}' is not a depth; 256 is used", System7ColorDepth.Indexed8);
            config.DepthApplications = display.Flag("apply_to_applications", config.DepthApplications);
            config.DepthDither = display.Flag("dither", config.DepthDither);
        }

        if (reader.Section("menubar") is { } menubar)
        {
            config.Clock = menubar.Flag("clock", config.Clock);
            config.ClockFormat = menubar.Text("clock_format") ?? config.ClockFormat;
        }

        if (reader.Section("keyboard") is { } keyboard)
        {
            config.Command = keyboard.Choice("command", "super", "super", "alt", "ctrl") switch
            {
                "alt" => Modifiers.Mod1,
                "ctrl" => Modifiers.Ctrl,
                _ => Modifiers.Mod4,
            };
            config.RepeatRate = Math.Clamp(keyboard.Number("repeat_rate", config.RepeatRate), 0, 60);
            config.RepeatDelay = Math.Clamp(keyboard.Number("repeat_delay", config.RepeatDelay), 100, 2000);
        }

        if (reader.Section("windows") is { } windows)
        {
            config.AppLayering = windows.Flag("app_layering", config.AppLayering);
            config.ClickThrough = windows.Flag("click_through", config.ClickThrough);
            config.XWayland = windows.Flag("xwayland", config.XWayland);
        }

        if (reader.Section("windowshade") is { } shade)
        {
            config.ShadeEnabled = shade.Flag("enabled", config.ShadeEnabled);
            config.ShadeClicks = Math.Clamp(shade.Number("clicks", config.ShadeClicks), 1, 3);
            config.ShadeModifier = shade.Choice("modifier", "none", "none", "command", "option", "control", "shift");
            config.ShadeSound = shade.Flag("sound", config.ShadeSound);
        }

        if (reader.Section("desktop") is { } desktop)
        {
            config.Pattern = desktop.Text("pattern") is { Length: > 0 } pattern ? pattern : config.Pattern;
            config.Wallpaper = desktop.Text("wallpaper") ?? "";
        }

        if (reader.Section("finder") is { } finder)
        {
            config.DiskName = finder.Text("disk_name") is { Length: > 0 } disk ? disk : config.DiskName;
            if (finder.Text("disk_root") is { Length: > 0 } root)
            {
                config.DiskRoot = root.StartsWith('~') ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), root[1..].TrimStart('/')) : root;
            }

            config.DoubleClickMs = Math.Clamp(finder.Number("double_click_ms", config.DoubleClickMs), 100, 2000);
        }

        if (reader.Section("mouse") is { } mouse && mouse.Raw("tracking") is { } tracking)
        {
            config.Tracking = tracking switch
            {
                double value => Math.Clamp(value, 0, 1),
                long value => Math.Clamp(value, 0, 1),
                _ => null,
            };
        }

        if (reader.Section("sound") is { } sound)
        {
            config.AlertSound = sound.Text("alert") ?? "";
            config.Volume = Math.Clamp(sound.Number("volume", config.Volume), -1, 7);
        }

        if (reader.Section("terminal") is { } terminal)
        {
            config.TerminalCommand = terminal.Text("command") ?? "";
            config.TerminalShell = terminal.Text("shell") ?? "";
            var size = terminal.Number("font_size", config.TerminalFontSize);
            config.TerminalFontSize = TerminalFonts.IsSize(size) ? size : Warn(log, $"[terminal] font_size {size} is not 9 or 12; 9 is used", 9);
            config.TerminalScrollback = Math.Clamp(terminal.Number("scrollback", config.TerminalScrollback), 0, 100000);
        }

        if (reader.Section("keybindings") is { } keybindings && keybindings.Raw("new_terminal") is string chord)
        {
            config.NewTerminal = chord.Length == 0
                ? null
                : HotkeyParser.TryParseChord(chord, log, out var keysym, out var modifiers)
                    ? new Hotkey(keysym, modifiers, "new_terminal", null)
                    : config.NewTerminal;
        }

        var rules = new List<System7Rule>();
        foreach (var row in reader.Sections("rule"))
        {
            var appIds = row.Words("app_id") ?? (row.Text("app_id") is { } one ? [one] : null);
            var prefixes = row.Words("app_id_prefix") ?? (row.Text("app_id_prefix") is { } prefix ? [prefix] : null);
            var chords = row.Choice("edit_chords", "ctrl", "ctrl", "ctrl+shift") == "ctrl+shift"
                ? Modifiers.Ctrl | Modifiers.Shift
                : Modifiers.Ctrl;
            rules.Add(new System7Rule
            {
                AppIds = appIds,
                AppIdPrefixes = prefixes,
                EditChords = chords,
                Decorations = row.Choice("decorations", "auto", "auto", "frame", "none"),
            });
        }

        config.Rules.AddRange(WindowRule.MostSpecificFirst(rules));
        reader.ReportUnknown();
        return config;
    }

    public System7Rule? RuleFor(string? appId)
    {
        foreach (var rule in Rules)
        {
            if (rule.MatchesText(appId, null))
            {
                return rule;
            }
        }

        return null;
    }

    public IEnumerable<string> RestartKeysChanged(System7Config next)
    {
        if (next.Command != Command)
        {
            yield return "keyboard.command";
        }

        if (next.XWayland != XWayland)
        {
            yield return "windows.xwayland";
        }
    }

    private static void Seed(string file, BasinLogger log)
    {
        try
        {
            if (System.IO.Path.GetDirectoryName(file) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write))
            {
                using var writer = new StreamWriter(stream);
                writer.Write(Template());
            }

            log.Info($"wrote the default configuration to {file}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            log.Warn($"cannot write {file}, keeping the built-in defaults: {error.Message}");
        }
    }

    private static T Warn<T>(BasinLogger log, string message, T value)
    {
        log.Warn($"{message}");
        return value;
    }
}
