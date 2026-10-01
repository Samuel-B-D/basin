using Basin.Diagnostics;
using Basin.Render.Skia;
using Basin.Themes.MsStyles;
using SkiaSharp;

namespace Basin.Frames.MsStyles;

public sealed class MsStylesFonts : IDisposable
{
    private static readonly BasinLogger Log = BasinLog.For("msstyles");

    private static readonly (string Suffix, int Weight)[] WeightSuffixes =
    [
        (" Bold", MsStylesFont.Bold),
        (" Semibold", 600),
        (" SemiBold", 600),
        (" Light", 300),
    ];

    private readonly Dictionary<(string Face, int Weight, bool Italic), SKTypeface> _resolved = [];
    private readonly HashSet<string> _logged = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SKTypeface> _owned = [];
    private readonly SKFontManager _manager;
    private bool _disposed;

    public MsStylesFonts(SKTypeface? fallback = null, SKFontManager? manager = null)
    {
        Fallback = fallback;
        _manager = manager ?? SKFontManager.Default;
    }

    public SKTypeface? Fallback { get; }

    public bool UseSystemFonts { get; init; } = true;

    public SKTypeface Resolve(in MsStylesFont font)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var key = (font.Face, font.Weight, font.Italic);
        if (_resolved.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var face = font.Face;
        var weight = font.Weight;
        foreach (var (suffix, suffixWeight) in WeightSuffixes)
        {
            if (face.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && face.Length > suffix.Length)
            {
                weight = Math.Max(weight, suffixWeight);
                break;
            }
        }

        var style = new SKFontStyle((SKFontStyleWeight)weight, SKFontStyleWidth.Normal, font.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        var typeface = UseSystemFonts ? Match(face, style) : null;
        if (typeface is null && UseSystemFonts)
        {
            var stem = face;
            foreach (var (suffix, _) in WeightSuffixes)
            {
                if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && stem.Length > suffix.Length)
                {
                    stem = stem[..^suffix.Length];
                    break;
                }
            }

            typeface = stem != face ? Match(stem, style) : null;
            if (typeface is null)
            {
                foreach (var alias in MsStylesFontAliases.For(face).Concat(stem != face ? MsStylesFontAliases.For(stem) : []))
                {
                    typeface = Match(alias, style);
                    if (typeface is not null)
                    {
                        Once(face, $"{face} is not installed; drawing its titles in {alias}");
                        break;
                    }
                }
            }
        }

        if (typeface is null && Fallback is not null)
        {
            typeface = Fallback;
            if (UseSystemFonts)
            {
                Once(face, $"{face} is not installed and has no installed alias; using the consumer font {Fallback.FamilyName}");
            }
        }

        if (typeface is null)
        {
            typeface = Track(_manager.MatchFamily("sans-serif", style)) ?? SKTypeface.Default;
            Once(face, $"{face} is not installed; using the default sans {typeface.FamilyName}");
        }

        _resolved[key] = typeface;
        return typeface;
    }

    private SKTypeface? Match(string family, SKFontStyle style)
    {
        var typeface = _manager.MatchFamily(family, style);
        if (typeface is null)
        {
            return null;
        }

        if (!string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase))
        {
            typeface.Dispose();
            return null;
        }

        return Track(typeface);
    }

    private SKTypeface? Track(SKTypeface? typeface)
    {
        if (typeface is not null)
        {
            _owned.Add(SkiaCensus.Track(typeface));
        }

        return typeface;
    }

    private void Once(string face, string message)
    {
        if (_logged.Add(face))
        {
            Log.Info($"{message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var typeface in _owned)
        {
            SkiaCensus.Release(typeface);
        }

        _owned.Clear();
        _resolved.Clear();
    }
}
