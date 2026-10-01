using System.Globalization;
using System.Text;
using Basin.Themes.MsStyles;
using SkiaSharp;

string? path = null;
string? color = null;
string? size = null;
string? images = null;
string? only = null;
string? mui = null;
var diagnostics = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--color": color = args[++i]; break;
        case "--size": size = args[++i]; break;
        case "--images": images = args[++i]; break;
        case "--class": only = args[++i]; break;
        case "--mui": mui = args[++i]; break;
        case "--diagnostics": diagnostics = true; break;
        case "-h":
        case "--help":
            Console.WriteLine("msstyles-dump THEME.msstyles [--color C] [--size S] [--images DIR] [--class NAME] [--mui FILE] [--diagnostics]");
            return 0;
        default:
            if (path is not null)
            {
                Console.Error.WriteLine($"unexpected argument: {args[i]}");
                return 1;
            }

            path = args[i];
            break;
    }
}

if (path is null)
{
    Console.Error.WriteLine("usage: msstyles-dump THEME.msstyles [--color C] [--size S] [--images DIR] [--class NAME] [--mui FILE] [--diagnostics]");
    return 1;
}

MsStylesTheme theme;
try
{
    theme = mui is null
        ? MsStylesTheme.Load(path, color, size)
        : MsStylesTheme.Parse(File.ReadAllBytes(path), color, size, File.ReadAllBytes(mui));
}
catch (Exception e) when (e is MsStylesFormatException or ArgumentException or IOException)
{
    Console.Error.WriteLine($"{path}: {e.Message}");
    return 1;
}

var output = new StringBuilder();
var invariant = CultureInfo.InvariantCulture;
output.AppendLine(invariant, $"; {Path.GetFileName(path)}: PACKTHEM {theme.Format}{(theme.HasMui ? ", strings from .mui" : string.Empty)}");
output.AppendLine(invariant, $"; colors: {string.Join(", ", theme.ColorSchemes.Select(c => c == theme.Color ? $"[{c}]" : c))}");
output.AppendLine(invariant, $"; sizes: {string.Join(", ", theme.Sizes.Select(s => s == theme.Size ? $"[{s}]" : s))}");
output.AppendLine(invariant, $"; {theme.Classes.Count} classes, {theme.Images.Count} images referenced");
output.AppendLine();

if (images is not null)
{
    Directory.CreateDirectory(images);
}

foreach (var cls in theme.Classes)
{
    if (only is not null && !string.Equals(cls.Name, only, StringComparison.OrdinalIgnoreCase) && !string.Equals(cls.FullName, only, StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    foreach (var (part, state) in cls.PartStates.OrderBy(b => b.Part).ThenBy(b => b.State))
    {
        output.AppendLine(invariant, $"[{SectionName(cls, part, state)}]");
        foreach (var id in cls.PropertiesOf(part, state))
        {
            output.AppendLine(invariant, $"{PropertyName(id)} = {Value(cls, part, state, id)}");
            if (images is not null && cls.TryGetImage(part, state, id, out var image))
            {
                WriteImage(Path.Combine(images, $"{cls.FullName.Replace("::", "-", StringComparison.Ordinal)}.{part}.{state}.{PropertyName(id)}.png"), image, null);
            }

            if (images is not null && id == ThemeProperty.AtlasRect &&
                cls.TryGetRect(part, state, id, out var rect) && cls.TryGetImage(0, 0, ThemeProperty.AtlasImage, out var atlas))
            {
                WriteImage(Path.Combine(images, $"{cls.FullName.Replace("::", "-", StringComparison.Ordinal)}.{part}.{state}.atlas.png"), atlas, rect);
            }
        }

        output.AppendLine();
    }
}

Console.Write(output.ToString());
if (diagnostics)
{
    Console.WriteLine($"; diagnostics: {theme.Diagnostics.Total}");
    foreach (var (key, count) in theme.Diagnostics.Counts.OrderBy(p => p.Key, StringComparer.Ordinal))
    {
        Console.WriteLine($";   {key} x{count}");
    }
}

return 0;

static string SectionName(MsStylesClass cls, int part, int state)
{
    var name = new StringBuilder(cls.FullName);
    if (part != 0)
    {
        name.Append('.').Append(MsStylesStyleMap.PartName(cls.Name, part) is { } partName ? Title(partName) : $"{part}");
    }

    if (state != 0)
    {
        name.Append('(').Append(MsStylesStyleMap.StateName(cls.Name, part, state) is { } stateName ? Title(stateName) : $"{state}").Append(')');
    }

    return name.ToString();
}

static string Title(string upper) => upper.Length == 0 ? upper : upper[0] + upper[1..].ToLowerInvariant();

static string PropertyName(ThemeProperty id) =>
    Enum.IsDefined(id) ? id.ToString() : $"Property{(int)id}";

static string Value(MsStylesClass cls, int part, int state, ThemeProperty id)
{
    var primitive = cls.PrimitiveOf(part, state, id);
    switch (primitive)
    {
        case ThemePrimitive.Enum:
            cls.TryGetInt(part, state, id, out var enumValue);
            return MsStylesStyleMap.EnumName(id, enumValue) is { } enumName ? Title(enumName) : enumValue.ToString(CultureInfo.InvariantCulture);
        case ThemePrimitive.Bool:
            cls.TryGetBool(part, state, id, out var flag);
            return flag ? "True" : "False";
        case ThemePrimitive.Int:
        case ThemePrimitive.Size:
            cls.TryGetInt(part, state, id, out var number);
            return number.ToString(CultureInfo.InvariantCulture);
        case ThemePrimitive.Color:
            cls.TryGetColor(part, state, id, out var c);
            return $"{c.R} {c.G} {c.B}";
        case ThemePrimitive.Margins:
            cls.TryGetMargins(part, state, id, out var m);
            return $"{m.Left}, {m.Right}, {m.Top}, {m.Bottom}";
        case ThemePrimitive.Rect:
            cls.TryGetRect(part, state, id, out var r);
            return $"{r.Left}, {r.Top}, {r.Right}, {r.Bottom}";
        case ThemePrimitive.Position:
            cls.TryGetPosition(part, state, id, out var p);
            return $"{p.X}, {p.Y}";
        case ThemePrimitive.IntList:
            cls.TryGetIntList(part, state, id, out var list);
            return string.Join(", ", list.ToArray());
        case ThemePrimitive.Font:
            cls.TryGetFont(part, state, id, out var f);
            return $"{f.Face}, {f.Points}{(f.Weight >= MsStylesFont.Bold ? ", bold" : string.Empty)}{(f.Italic ? ", italic" : string.Empty)}{(f.Underline ? ", underline" : string.Empty)}";
        case ThemePrimitive.String:
        case ThemePrimitive.Filename:
            cls.TryGetString(part, state, id, out var text);
            return text;
        case ThemePrimitive.DiskStream:
            return cls.TryGetImage(part, state, id, out var stream) ? stream.Name : "(missing)";
        default:
            return $"({primitive})";
    }
}

static void WriteImage(string file, MsStylesImage image, MsStylesRect? crop)
{
    using var bitmap = SKBitmap.Decode(image.Bytes.ToArray());
    if (bitmap is null)
    {
        return;
    }

    using var source = SKImage.FromBitmap(bitmap);
    using var cropped = crop is { } rect ? source.Subset(new SKRectI(rect.Left, rect.Top, rect.Right, rect.Bottom)) : null;
    using var data = (cropped ?? source).Encode(SKEncodedImageFormat.Png, 100);
    if (data is null)
    {
        return;
    }

    using var stream = File.Create(file);
    data.SaveTo(stream);
}
