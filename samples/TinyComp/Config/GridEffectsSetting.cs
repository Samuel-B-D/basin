using Basin;
using Basin.Config;
using Basin.Diagnostics;
using Basin.Effects;
using Tomlyn.Model;

namespace TinyComp;

internal sealed class GridEffectsSetting
{
    public const uint DefaultHighlightColor = 0xe8ecffff;

    public const double DefaultHighlightRadius = 160.0;

    public const double DefaultHighlightSpring = 300.0;

    public const double DefaultHighlightDamping = 1.0;

    public const double DefaultDensitySpacing = 12.0;

    public const double DefaultDensityFloor = 0.15;

    public const double DefaultGlowRadius = 6.0;

    public const double DefaultHighlightGlow = 2.0;

    public double? Glow { get; init; }

    public double? GlowRadius { get; init; }

    public GridGlowPath? GlowPath { get; init; }

    public double? HighlightGlow { get; init; }

    public const double DefaultPinchReach = 400.0;

    public const double DefaultPinchSpeed = 600.0;

    public const double DefaultPinchSpring = 200.0;

    public const double DefaultPinchDamping = 1.1;

    public double? Pinch { get; init; }

    public double? PinchReach { get; init; }

    public double? PinchSpeed { get; init; }

    public double? PinchSpring { get; init; }

    public double? PinchDamping { get; init; }

    public double PinchValue => Pinch ?? 0.0;

    public double PinchReachValue => PinchReach ?? DefaultPinchReach;

    public double PinchSpeedValue => PinchSpeed ?? DefaultPinchSpeed;

    public double PinchSpringValue => PinchSpring ?? DefaultPinchSpring;

    public double PinchDampingValue => PinchDamping ?? DefaultPinchDamping;

    public uint? HighlightColor { get; init; }

    public double? HighlightRadius { get; init; }

    public double? HighlightDragged { get; init; }

    public double? HighlightFocused { get; init; }

    public double? HighlightWindows { get; init; }

    public double? HighlightPointer { get; init; }

    public double? HighlightSpring { get; init; }

    public double? HighlightDamping { get; init; }

    public bool? Density { get; init; }

    public double? DensitySpacing { get; init; }

    public double? DensityFloor { get; init; }

    public static GridEffectsSetting Defaults { get; } = new()
    {
        Glow = 0.0,
        GlowRadius = DefaultGlowRadius,
        GlowPath = GridGlowPath.Auto,
        HighlightGlow = DefaultHighlightGlow,
        Pinch = 0.0,
        PinchReach = DefaultPinchReach,
        PinchSpeed = DefaultPinchSpeed,
        PinchSpring = DefaultPinchSpring,
        PinchDamping = DefaultPinchDamping,
        HighlightColor = DefaultHighlightColor,
        HighlightRadius = DefaultHighlightRadius,
        HighlightDragged = 0.0,
        HighlightFocused = 0.0,
        HighlightWindows = 0.0,
        HighlightPointer = 0.0,
        HighlightSpring = DefaultHighlightSpring,
        HighlightDamping = DefaultHighlightDamping,
        Density = false,
        DensitySpacing = DefaultDensitySpacing,
        DensityFloor = DefaultDensityFloor,
    };

    public double GlowValue => Glow ?? 0.0;

    public double GlowRadiusValue => GlowRadius ?? DefaultGlowRadius;

    public GridGlowPath GlowPathValue => GlowPath ?? GridGlowPath.Auto;

    public string GlowPathName => NameOf(GlowPathValue);

    public double HighlightGlowValue => HighlightGlow ?? DefaultHighlightGlow;

    public uint HighlightRgba => HighlightColor ?? DefaultHighlightColor;

    public double HighlightRadiusValue => HighlightRadius ?? DefaultHighlightRadius;

    public double HighlightDraggedValue => HighlightDragged ?? 0.0;

    public double HighlightFocusedValue => HighlightFocused ?? 0.0;

    public double HighlightWindowsValue => HighlightWindows ?? 0.0;

    public double HighlightPointerValue => HighlightPointer ?? 0.0;

    public double HighlightSpringValue => HighlightSpring ?? DefaultHighlightSpring;

    public double HighlightDampingValue => HighlightDamping ?? DefaultHighlightDamping;

    public bool DensityValue => Density ?? false;

    public double DensitySpacingValue => DensitySpacing ?? DefaultDensitySpacing;

    public double DensityFloorValue => DensityFloor ?? DefaultDensityFloor;

    public bool Highlights =>
        HighlightRadiusValue > 0 &&
        (HighlightDraggedValue > 0 || HighlightFocusedValue > 0 || HighlightWindowsValue > 0 || HighlightPointerValue > 0);

    public RenderColor HighlightRenderColor
    {
        get
        {
            var rgba = HighlightRgba;
            var a = (rgba & 0xFF) / 255f;
            return new RenderColor(
                ((rgba >> 24) & 0xFF) / 255f * a,
                ((rgba >> 16) & 0xFF) / 255f * a,
                ((rgba >> 8) & 0xFF) / 255f * a,
                a);
        }
    }

    public GridEffects Effects => EffectsOn(GlowPathValue is GridGlowPath.Auto or GridGlowPath.Halo ? GridGlowPath.Halo : GridGlowPath.None);

    public GridEffects EffectsOn(GridGlowPath path) => GridEffects.None with
    {
        Density = DensityValue,
        DensitySpacing = (float)DensitySpacingValue,
        DensityFloor = (float)DensityFloorValue,
        Glow = (float)GlowValue,
        GlowRadius = (float)GlowRadiusValue,
        GlowPath = path,
    };

    public static string NameOf(GridGlowPath path) => path switch
    {
        GridGlowPath.Shader => "shader",
        GridGlowPath.Halo => "halo",
        GridGlowPath.None => "none",
        _ => "auto",
    };

    public static GridGlowPath? GlowPathFromName(string name) => name switch
    {
        "auto" => GridGlowPath.Auto,
        "shader" => GridGlowPath.Shader,
        "halo" => GridGlowPath.Halo,
        "none" => GridGlowPath.None,
        _ => null,
    };

    public GridEffectsSetting Over(GridEffectsSetting? fallback) => fallback is null ? this : new()
    {
        Glow = Glow ?? fallback.Glow,
        GlowRadius = GlowRadius ?? fallback.GlowRadius,
        GlowPath = GlowPath ?? fallback.GlowPath,
        HighlightGlow = HighlightGlow ?? fallback.HighlightGlow,
        Pinch = Pinch ?? fallback.Pinch,
        PinchReach = PinchReach ?? fallback.PinchReach,
        PinchSpeed = PinchSpeed ?? fallback.PinchSpeed,
        PinchSpring = PinchSpring ?? fallback.PinchSpring,
        PinchDamping = PinchDamping ?? fallback.PinchDamping,
        HighlightColor = HighlightColor ?? fallback.HighlightColor,
        HighlightRadius = HighlightRadius ?? fallback.HighlightRadius,
        HighlightDragged = HighlightDragged ?? fallback.HighlightDragged,
        HighlightFocused = HighlightFocused ?? fallback.HighlightFocused,
        HighlightWindows = HighlightWindows ?? fallback.HighlightWindows,
        HighlightPointer = HighlightPointer ?? fallback.HighlightPointer,
        HighlightSpring = HighlightSpring ?? fallback.HighlightSpring,
        HighlightDamping = HighlightDamping ?? fallback.HighlightDamping,
        Density = Density ?? fallback.Density,
        DensitySpacing = DensitySpacing ?? fallback.DensitySpacing,
        DensityFloor = DensityFloor ?? fallback.DensityFloor,
    };

    public static GridEffectsSetting Parse(TomlTable table, string section, BasinLogger log)
    {
        double? glow = null;
        double? glowRadius = null;
        GridGlowPath? glowPath = null;
        double? highlightGlow = null;
        double? pinch = null;
        double? pinchReach = null;
        double? pinchSpeed = null;
        double? pinchSpring = null;
        double? pinchDamping = null;
        uint? highlightColor = null;
        double? highlightRadius = null;
        double? dragged = null;
        double? focused = null;
        double? windows = null;
        double? pointer = null;
        double? spring = null;
        double? damping = null;
        bool? density = null;
        double? densitySpacing = null;
        double? densityFloor = null;
        foreach (var (key, value) in table)
        {
            switch (key)
            {
                case "glow" when Number(value) is { } strength:
                    glow = Clamp(section, key, strength, 0.0, 1.0, log);
                    break;
                case "glow_radius" when Number(value) is { } radius:
                    glowRadius = Clamp(section, key, radius, 1.0, 64.0, log);
                    break;
                case "glow_path" when value is string pathName:
                    glowPath = GlowPathFromName(pathName);
                    if (glowPath is null)
                    {
                        log.Warn($"[{section}] glow_path \"{pathName}\" is not auto|shader|halo|none, keeping auto");
                        glowPath = GridGlowPath.Auto;
                    }

                    break;
                case "highlight_glow" when Number(value) is { } boost:
                    highlightGlow = Clamp(section, key, boost, 0.0, 8.0, log);
                    break;
                case "pinch" when Number(value) is { } slope:
                    pinch = Clamp(section, key, slope, -0.9, 4.0, log);
                    break;
                case "pinch_reach" when Number(value) is { } reach:
                    pinchReach = Clamp(section, key, reach, 0.0, 4096.0, log);
                    break;
                case "pinch_speed" when Number(value) is { } speed:
                    pinchSpeed = Clamp(section, key, speed, 0.0, 20000.0, log);
                    break;
                case "pinch_spring" when Number(value) is { } constant:
                    pinchSpring = Clamp(section, key, constant, 1.0, 5000.0, log);
                    break;
                case "pinch_damping" when Number(value) is { } ratio:
                    pinchDamping = Clamp(section, key, ratio, 0.1, 4.0, log);
                    break;
                case "highlight_color" when TomlColor.Rgba(value) is { } rgba:
                    highlightColor = rgba;
                    break;
                case "highlight_radius" when Number(value) is { } radius:
                    highlightRadius = Clamp(section, key, radius, 0.0, 4096.0, log);
                    break;
                case "highlight_dragged" when Number(value) is { } strength:
                    dragged = Clamp(section, key, strength, 0.0, 1.0, log);
                    break;
                case "highlight_focused" when Number(value) is { } strength:
                    focused = Clamp(section, key, strength, 0.0, 1.0, log);
                    break;
                case "highlight_windows" when Number(value) is { } strength:
                    windows = Clamp(section, key, strength, 0.0, 1.0, log);
                    break;
                case "highlight_pointer" when Number(value) is { } strength:
                    pointer = Clamp(section, key, strength, 0.0, 1.0, log);
                    break;
                case "highlight_spring" when Number(value) is { } constant:
                    spring = Clamp(section, key, constant, 1.0, 5000.0, log);
                    break;
                case "highlight_damping" when Number(value) is { } ratio:
                    damping = Clamp(section, key, ratio, 0.1, 4.0, log);
                    break;
                case "density" when value is bool flag:
                    density = flag;
                    break;
                case "density_spacing" when Number(value) is { } spacing:
                    densitySpacing = Clamp(section, key, spacing, 1.0, 256.0, log);
                    break;
                case "density_floor" when Number(value) is { } floor:
                    densityFloor = Clamp(section, key, floor, 0.0, 1.0, log);
                    break;
                default:
                    log.Warn($"[{section}] {key}: unknown key or wrong type, ignored");
                    break;
            }
        }

        return new GridEffectsSetting
        {
            Glow = glow,
            GlowRadius = glowRadius,
            GlowPath = glowPath,
            HighlightGlow = highlightGlow,
            Pinch = pinch,
            PinchReach = pinchReach,
            PinchSpeed = pinchSpeed,
            PinchSpring = pinchSpring,
            PinchDamping = pinchDamping,
            HighlightColor = highlightColor,
            HighlightRadius = highlightRadius,
            HighlightDragged = dragged,
            HighlightFocused = focused,
            HighlightWindows = windows,
            HighlightPointer = pointer,
            HighlightSpring = spring,
            HighlightDamping = damping,
            Density = density,
            DensitySpacing = densitySpacing,
            DensityFloor = densityFloor,
        };
    }

    private static double Clamp(string section, string key, double value, double low, double high, BasinLogger log)
    {
        var clamped = Math.Clamp(value, low, high);
        if (clamped != value)
        {
            log.Warn($"[{section}] {key} {value} is outside {low} to {high}, clamping it to {clamped}");
        }

        return clamped;
    }

    private static double? Number(object value) => value switch
    {
        double fraction => fraction,
        long whole => whole,
        _ => null,
    };
}
