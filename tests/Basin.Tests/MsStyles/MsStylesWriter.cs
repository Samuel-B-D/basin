using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Basin.Themes.MsStyles;
using SkiaSharp;

namespace Basin.Tests;

internal static class MsStylesWriter
{
    public static string Root => Path.Combine(AppContext.BaseDirectory, "Fixtures", "msstyles");

    public static readonly string[] PlainColors = ["Blue", "Green"];

    public static readonly string[] PlainSizes = ["NormalSize", "LargeFonts"];

    public static byte[] Plain()
    {
        var directory = Path.Combine(Root, "Plain");
        var writer = new PeImageWriter()
            .Add("PACKTHEM_VERSION", 1, [3, 0])
            .Add("COLORNAMES", 1, MultiString(PlainColors))
            .Add("SIZENAMES", 1, MultiString(PlainSizes))
            .Add("FILERESNAMES", 1, MultiString(PlainColors.SelectMany(c => PlainSizes.Select(s => VariantName(c, s)))))
            .Add("ORIGFILENAMES", 1, MultiString(["NormalSize.ini", "LargeFonts.ini"]))
            .Add("TEXTFILE", "THEMES_INI", Utf16(File.ReadAllText(Path.Combine(directory, "themes.ini"))));
        foreach (var color in PlainColors)
        {
            foreach (var size in PlainSizes)
            {
                writer.Add("TEXTFILE", VariantName(color, size), Utf16(PlainIni(size, color)));
            }

            foreach (var png in Directory.EnumerateFiles(Path.Combine(directory, color), "*.png"))
            {
                var name = $"{color}_{Path.GetFileNameWithoutExtension(png)}_BMP".ToUpperInvariant();
                writer.Add(PeResources.BitmapType, name, Dib(png));
            }
        }

        writer.AddStrings(new Dictionary<int, string> { [5000] = "Plain", [5003] = "basin" });
        return writer.Build();
    }

    public static byte[] V3(string ini, IReadOnlyDictionary<string, byte[]>? bitmaps = null)
    {
        var directory = Path.Combine(Root, "Plain");
        var writer = new PeImageWriter()
            .Add("PACKTHEM_VERSION", 1, [3, 0])
            .Add("COLORNAMES", 1, MultiString(["Blue"]))
            .Add("SIZENAMES", 1, MultiString(["NormalSize"]))
            .Add("FILERESNAMES", 1, MultiString([VariantName("Blue", "NormalSize")]))
            .Add("TEXTFILE", VariantName("Blue", "NormalSize"), Utf16(ini));
        foreach (var png in Directory.EnumerateFiles(Path.Combine(directory, "Blue"), "*.png"))
        {
            writer.Add(PeResources.BitmapType, $"BLUE_{Path.GetFileNameWithoutExtension(png)}_BMP".ToUpperInvariant(), Dib(png));
        }

        foreach (var (name, dib) in bitmaps ?? new Dictionary<string, byte[]>())
        {
            writer.Add(PeResources.BitmapType, MsStylesValueParser.ResourceNameOf(name), dib);
        }

        return writer.Build();
    }

    public static string PlainIni(string size, string color) =>
        File.ReadAllText(Path.Combine(Root, "Plain", size + ".ini")).Replace("%COLOR%", color, StringComparison.Ordinal);

    public static byte[] PlainAsV4(string color = "Blue", string size = "NormalSize")
    {
        var ini = PlainIni(size, color).Replace("[SysMetrics]", "[sysmetrics]", StringComparison.Ordinal);
        var directory = Path.Combine(Root, "Plain");
        return V4(ini, directory, size, color, file => Path.Combine(directory, Path.ChangeExtension(file.Replace('\\', '/'), ".png")));
    }

    public static byte[] Glassy()
    {
        var directory = Path.Combine(Root, "Glassy");
        return V4(File.ReadAllText(Path.Combine(directory, "variant.ini")), directory, "NormalSize", "NormalColor", file => Path.Combine(directory, file));
    }

    public static byte[] V4(string ini, string directory, string size, string color, Func<string, string> imagePath)
    {
        var sections = Sections(ini);
        var classNames = new List<string>();
        var strings = new Dictionary<int, string>();
        var images = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var writer = new PeImageWriter().Add("PACKTHEM_VERSION", 1, [4, 0]);
        using var variant = new MemoryStream();
        using var rmap = new MemoryStream();
        foreach (var (name, values) in sections)
        {
            if (string.Equals(name, "strings", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var (key, value) in values)
                {
                    strings[int.Parse(key, CultureInfo.InvariantCulture)] = value;
                }

                continue;
            }

            var (fullClass, part, state) = SectionOf(name);
            var classIndex = classNames.FindIndex(c => string.Equals(c, fullClass, StringComparison.OrdinalIgnoreCase));
            if (classIndex < 0)
            {
                classNames.Add(fullClass);
                classIndex = classNames.Count - 1;
            }

            var target = string.Equals(fullClass, "documentation", StringComparison.OrdinalIgnoreCase) ? rmap : variant;
            foreach (var (key, value) in values)
            {
                if (string.Equals(key, "AtlasImage", StringComparison.OrdinalIgnoreCase))
                {
                    var png = File.ReadAllBytes(Path.Combine(directory, value));
                    writer.Add("STREAM", 850, png);
                    Record(target, (int)ThemePrimitive.DiskStream, ThemePrimitive.DiskStream, classIndex, part, state, 850, []);
                    continue;
                }

                if (!MsStylesStyleMap.TryLookupProperty(key, out var property))
                {
                    throw new InvalidOperationException($"Unknown property {key} in [{name}].");
                }

                if (value.StartsWith('@'))
                {
                    Record(target, (int)property.Id, property.Primitive, classIndex, part, state, int.Parse(value[1..], CultureInfo.InvariantCulture), []);
                    continue;
                }

                if (property.Primitive == ThemePrimitive.Filename)
                {
                    if (!images.TryGetValue(value, out var id))
                    {
                        id = 100 + images.Count;
                        images[value] = id;
                        writer.Add("IMAGE", id, File.ReadAllBytes(imagePath(value)));
                    }

                    Record(target, (int)property.Id, property.Primitive, classIndex, part, state, id, []);
                    continue;
                }

                Record(target, (int)property.Id, property.Primitive, classIndex, part, state, 0, Payload(property, value));
            }
        }

        var cmap = new StringBuilder();
        for (var i = 0; i < classNames.Count; i++)
        {
            cmap.Append(classNames[i]).Append('\0');
            if (i == 0)
            {
                cmap.Append('\0').Append('\0');
            }
        }

        writer.Add("CMAP", "CMAP", Encoding.Unicode.GetBytes(cmap.ToString()))
            .Add("VMAP", "VMAP", VariantMap(["Normal", size, color]))
            .Add("VARIANT", "NORMAL", variant.ToArray())
            .Add("MINCOLORDEPTH", 1, [15, 0]);
        if (rmap.Length > 0)
        {
            writer.Add("RMAP", "RMAP", rmap.ToArray());
        }

        if (strings.Count > 0)
        {
            writer.AddStrings(strings);
        }

        return writer.Build();
    }

    public static void Record(Stream target, int id, ThemePrimitive primitive, int classIndex, int part, int state, int resourceId, byte[] payload)
    {
        Span<byte> header = stackalloc byte[MsStylesVariantReader.HeaderSize];
        BinaryPrimitives.WriteInt32LittleEndian(header, id);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], (int)primitive);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], classIndex);
        BinaryPrimitives.WriteInt32LittleEndian(header[12..], part);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..], state);
        BinaryPrimitives.WriteInt32LittleEndian(header[20..], resourceId);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..], resourceId != 0 ? Math.Max(payload.Length, 4) : payload.Length);
        target.Write(header);
        if (resourceId == 0)
        {
            target.Write(payload);
            var padding = ((payload.Length + 7) & ~7) - payload.Length;
            target.Write(new byte[padding]);
        }
    }

    public static byte[] Dib(string png)
    {
        using var decoded = SKBitmap.Decode(png) ?? throw new InvalidOperationException($"Cannot decode {png}.");
        using var bitmap = new SKBitmap(new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        decoded.CopyTo(bitmap, SKColorType.Rgba8888);
        var pixels = bitmap.GetPixelSpan();
        var hasAlpha = false;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            hasAlpha |= pixels[i] != 255;
        }

        var bpp = hasAlpha ? 32 : 24;
        var stride = (decoded.Width * (bpp / 8) + 3) & ~3;
        var dib = new byte[40 + stride * decoded.Height];
        var span = dib.AsSpan();
        BinaryPrimitives.WriteInt32LittleEndian(span, 40);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], decoded.Width);
        BinaryPrimitives.WriteInt32LittleEndian(span[8..], decoded.Height);
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[14..], (ushort)bpp);
        BinaryPrimitives.WriteInt32LittleEndian(span[20..], stride * decoded.Height);
        for (var y = 0; y < decoded.Height; y++)
        {
            var row = 40 + (decoded.Height - 1 - y) * stride;
            for (var x = 0; x < decoded.Width; x++)
            {
                var source = (y * decoded.Width + x) * 4;
                var target = row + x * (bpp / 8);
                dib[target] = pixels[source + 2];
                dib[target + 1] = pixels[source + 1];
                dib[target + 2] = pixels[source];
                if (bpp == 32)
                {
                    dib[target + 3] = pixels[source + 3];
                }
            }
        }

        return dib;
    }

    public static byte[] Dib(int width, int height, uint[] argb, bool alpha)
    {
        var bpp = alpha ? 32 : 24;
        var stride = (width * (bpp / 8) + 3) & ~3;
        var dib = new byte[40 + stride * height];
        BinaryPrimitives.WriteInt32LittleEndian(dib, 40);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(4), width);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(8), height);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(14), (ushort)bpp);
        for (var y = 0; y < height; y++)
        {
            var row = 40 + (height - 1 - y) * stride;
            for (var x = 0; x < width; x++)
            {
                var pixel = argb[y * width + x];
                var target = row + x * (bpp / 8);
                dib[target] = (byte)pixel;
                dib[target + 1] = (byte)(pixel >> 8);
                dib[target + 2] = (byte)(pixel >> 16);
                if (alpha)
                {
                    dib[target + 3] = (byte)(pixel >> 24);
                }
            }
        }

        return dib;
    }

    public static List<(string Name, List<(string Key, string Value)> Values)> Sections(string ini)
    {
        var sections = new List<(string, List<(string, string)>)>();
        List<(string, string)>? current = null;
        foreach (var raw in ini.Split('\n'))
        {
            var line = raw;
            var comment = line.IndexOf(';');
            if (comment >= 0)
            {
                line = line[..comment];
            }

            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line[0] == '[')
            {
                current = [];
                sections.Add((line.Trim('[', ']').Trim(), current));
                continue;
            }

            var equals = line.IndexOf('=');
            current!.Add((line[..equals].Trim(), line[(equals + 1)..].Trim()));
        }

        return sections;
    }

    private static (string Class, int Part, int State) SectionOf(string name)
    {
        if (!MsStylesIniReader.TryParseSectionName(name, out var app, out var className, out var partName, out var stateName))
        {
            throw new InvalidOperationException($"Bad section [{name}].");
        }

        var part = 0;
        if (partName is not null && !int.TryParse(partName, out part) &&
            !MsStylesStyleMap.TryLookupPartState(className, partName, null, out part, out _))
        {
            throw new InvalidOperationException($"Unknown part in [{name}].");
        }

        var state = 0;
        if (stateName is not null && !int.TryParse(stateName, out state) &&
            !MsStylesStyleMap.TryLookupPartState(className, partName is not null && !int.TryParse(partName, out _) ? partName : null, stateName, out _, out state))
        {
            throw new InvalidOperationException($"Unknown state in [{name}].");
        }

        return (app.Length == 0 ? className : app + "::" + className, part, state);
    }

    private static byte[] Payload(MsStylesPropertyName property, string value)
    {
        var position = 0;
        int Next()
        {
            if (!MsStylesValueParser.TryNextInteger(value, ref position, out var number))
            {
                throw new InvalidOperationException($"{property.Name} = {value} needs more integers.");
            }

            return number;
        }

        byte[] Ints(params int[] values)
        {
            var bytes = new byte[values.Length * 4];
            for (var i = 0; i < values.Length; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4), values[i]);
            }

            return bytes;
        }

        switch (property.Primitive)
        {
            case ThemePrimitive.Enum:
                return Ints(MsStylesStyleMap.TryLookupEnum(property.Id, value, out var enumValue) ? enumValue : Next());
            case ThemePrimitive.Bool:
                return Ints(MsStylesValueParser.ParseBool(value) ? 1 : 0);
            case ThemePrimitive.Int:
            case ThemePrimitive.Size:
                return Ints(Next());
            case ThemePrimitive.Color:
                return Ints(new MsStylesColor((byte)Next(), (byte)Next(), (byte)Next()).ToColorRef());
            case ThemePrimitive.Margins:
            case ThemePrimitive.Rect:
                return Ints(Next(), Next(), Next(), Next());
            case ThemePrimitive.Position:
                return Ints(Next(), Next());
            case ThemePrimitive.IntList:
                var list = MsStylesValueParser.Integers(value, MsStylesValueParser.MaxIntList);
                return Ints([list.Length, .. list]);
            case ThemePrimitive.String:
                return Encoding.Unicode.GetBytes(value + "\0");
            case ThemePrimitive.Font:
                return LogFont(value);
            default:
                throw new InvalidOperationException($"{property.Name} has no payload form.");
        }
    }

    private static byte[] LogFont(string value)
    {
        if (!MsStylesValueParser.TryParseFont(value, out var font))
        {
            throw new InvalidOperationException($"Bad font {value}.");
        }

        var bytes = new byte[92];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, -(int)Math.Round(font.Points * 96.0 / 72.0));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), font.Weight);
        bytes[20] = font.Italic ? (byte)1 : (byte)0;
        bytes[21] = font.Underline ? (byte)1 : (byte)0;
        bytes[22] = font.StrikeOut ? (byte)1 : (byte)0;
        Encoding.Unicode.GetBytes(font.Face).CopyTo(bytes.AsSpan(28));
        return bytes;
    }

    private static byte[] VariantMap(IReadOnlyList<string> names)
    {
        using var stream = new MemoryStream();
        foreach (var name in names)
        {
            var bytes = new byte[4 + (name.Length + 1) * 2];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, name.Length + 1);
            Encoding.Unicode.GetBytes(name).CopyTo(bytes.AsSpan(4));
            stream.Write(bytes);
            stream.Write(new byte[((bytes.Length + 3) & ~3) - bytes.Length]);
        }

        return stream.ToArray();
    }

    private static string VariantName(string color, string size) => $"{size}{color}_INI".ToUpperInvariant();

    private static byte[] MultiString(IEnumerable<string> names) =>
        Encoding.Unicode.GetBytes(string.Concat(names.Select(n => n + "\0")) + "\0");

    private static byte[] Utf16(string text) => Encoding.Unicode.GetBytes(text);
}
