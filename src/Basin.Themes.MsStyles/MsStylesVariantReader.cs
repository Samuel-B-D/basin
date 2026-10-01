using System.Buffers.Binary;
using System.Text;

namespace Basin.Themes.MsStyles;

internal static class MsStylesVariantReader
{
    public const int HeaderSize = 32;

    public static void Read(MsStylesTheme theme, string? color, string? size)
    {
        var resources = theme.Resources;
        if (!resources.TryFind("CMAP", "CMAP", out var cmap))
        {
            throw new MsStylesFormatException("A PACKTHEM 4 file needs a CMAP resource.");
        }

        var classNames = ClassNames(cmap.Span);
        var variants = VariantMap(resources);
        var colors = variants.Select(v => v.Color).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var sizes = variants.Select(v => v.Size).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var colorIndex = colors.Count > 0 ? MsStylesIniReader.Select(colors, color, "color scheme") : 0;
        var sizeIndex = sizes.Count > 0 ? MsStylesIniReader.Select(sizes, size, "size") : 0;
        var selectedColor = colors.Count > 0 ? colors[colorIndex] : string.Empty;
        var selectedSize = sizes.Count > 0 ? sizes[sizeIndex] : string.Empty;
        theme.SetVariants(colors, sizes, selectedColor, selectedSize);

        var variantNames = resources.Names("VARIANT");
        if (variantNames.Count == 0)
        {
            throw new MsStylesFormatException("A PACKTHEM 4 file needs a VARIANT resource.");
        }

        var chosen = variantNames[0];
        foreach (var (name, variantSize, variantColor) in variants)
        {
            if (string.Equals(variantSize, selectedSize, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(variantColor, selectedColor, StringComparison.OrdinalIgnoreCase) &&
                variantNames.Contains(new PeResourceKey(name)))
            {
                chosen = new PeResourceKey(name);
                break;
            }
        }

        resources.TryFind("VARIANT", chosen, out var blob);
        ReadRecords(theme, classNames, blob.Span, "VARIANT");
        if (resources.TryFind("RMAP", "RMAP", out var rmap))
        {
            ReadRecords(theme, classNames, rmap.Span, "RMAP");
        }

        resources.TryFind("BCMAP", "BCMAP", out var bcmap);
        resources.TryFind("AMAP", "AMAP", out var amap);
        theme.SetMaps(bcmap, rmap, amap);
    }

    public static List<string> ClassNames(ReadOnlySpan<byte> cmap)
    {
        var names = new List<string>();
        foreach (var name in Encoding.Unicode.GetString(cmap).Split('\0'))
        {
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }

        return names;
    }

    public static List<(string Name, string Size, string Color)> VariantMap(PeResources resources)
    {
        var strings = new List<string>();
        if (resources.TryFind("VMAP", "VMAP", out var vmap))
        {
            var span = vmap.Span;
            var position = 0;
            while (position + 4 <= span.Length)
            {
                var length = BinaryPrimitives.ReadInt32LittleEndian(span[position..]);
                position += 4;
                if (length <= 0 || position + length * 2 > span.Length)
                {
                    break;
                }

                strings.Add(Encoding.Unicode.GetString(span.Slice(position, length * 2)).TrimEnd('\0'));
                position += length * 2;
                position = (position + 3) & ~3;
            }
        }

        var variants = new List<(string, string, string)>();
        for (var i = 0; i + 2 < strings.Count; i += 3)
        {
            variants.Add((strings[i], strings[i + 1], strings[i + 2]));
        }

        return variants;
    }

    public static void ReadRecords(MsStylesTheme theme, IReadOnlyList<string> classNames, ReadOnlySpan<byte> blob, string source)
    {
        var diagnostics = theme.Diagnostics;
        var offset = 0;
        while (offset < blob.Length)
        {
            if (offset + HeaderSize > blob.Length)
            {
                throw new MsStylesFormatException($"{source} record header runs past the end.", offset);
            }

            var record = blob[offset..];
            var id = BinaryPrimitives.ReadInt32LittleEndian(record);
            var primitive = (ThemePrimitive)BinaryPrimitives.ReadInt32LittleEndian(record[4..]);
            var classIndex = BinaryPrimitives.ReadInt32LittleEndian(record[8..]);
            var part = BinaryPrimitives.ReadInt32LittleEndian(record[12..]);
            var state = BinaryPrimitives.ReadInt32LittleEndian(record[16..]);
            var resourceId = BinaryPrimitives.ReadInt32LittleEndian(record[20..]);
            var size = BinaryPrimitives.ReadInt32LittleEndian(record[28..]);
            if (size < 0)
            {
                throw new MsStylesFormatException($"{source} record has a negative size.", offset);
            }

            var payloadLength = resourceId == 0 ? (size + 7) & ~7 : 0;
            if (offset + HeaderSize + payloadLength > blob.Length || (resourceId == 0 && offset + HeaderSize + size > blob.Length))
            {
                throw new MsStylesFormatException($"{source} record payload runs past the end.", offset);
            }

            var payload = resourceId == 0 ? record.Slice(HeaderSize, size) : ReadOnlySpan<byte>.Empty;
            offset += HeaderSize + payloadLength;

            if ((uint)classIndex >= (uint)classNames.Count)
            {
                diagnostics.Add("class-index", classIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
                continue;
            }

            if (!Enum.IsDefined((ThemeProperty)id))
            {
                diagnostics.Add("property-id", id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            var fullName = classNames[classIndex];
            var appSeparator = fullName.IndexOf("::", StringComparison.Ordinal);
            var app = appSeparator >= 0 ? fullName[..appSeparator] : string.Empty;
            var className = appSeparator >= 0 ? fullName[(appSeparator + 2)..] : fullName;
            var block = theme.AddClass(app, className).Block(part, state);
            if (block.Properties.ContainsKey(id))
            {
                continue;
            }

            var isGlobal = app.Length == 0 &&
                (string.Equals(className, "globals", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(className, MsStylesIniReader.SysMetricsClass, StringComparison.OrdinalIgnoreCase));
            var origin = state != 0 ? PropertyOrigin.State
                : part != 0 ? PropertyOrigin.Part
                : isGlobal ? PropertyOrigin.Global
                : PropertyOrigin.Class;
            if (Decode(theme, id, primitive, origin, resourceId, payload) is { } property)
            {
                block.TryAdd(property);
            }
        }
    }

    private static MsStylesProperty? Decode(MsStylesTheme theme, int id, ThemePrimitive primitive, PropertyOrigin origin, int resourceId, ReadOnlySpan<byte> payload)
    {
        var property = new MsStylesProperty(id, primitive, origin, 4) { ResourceId = resourceId };
        var diagnostics = theme.Diagnostics;
        string? resourceText = null;
        if (resourceId != 0 && primitive is not (ThemePrimitive.Filename or ThemePrimitive.DiskStream))
        {
            if (!theme.TryGetString(resourceId, out var text))
            {
                diagnostics.Add("string", resourceId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return null;
            }

            resourceText = text;
        }

        switch (primitive)
        {
            case ThemePrimitive.Enum:
            case ThemePrimitive.Int:
            case ThemePrimitive.Size:
                if (resourceText is not null)
                {
                    property.Values = MsStylesValueParser.Integers(resourceText, 1);
                }
                else if (payload.Length >= 4)
                {
                    property.Values = [BinaryPrimitives.ReadInt32LittleEndian(payload)];
                }

                return property.Values.Length == 1 ? property : Bad(diagnostics, id, primitive);
            case ThemePrimitive.Bool:
                if (resourceText is not null)
                {
                    property.Values = [MsStylesValueParser.ParseLocalizedBool(resourceText) ? 1 : 0];
                }
                else if (payload.Length >= 4)
                {
                    property.Values = [BinaryPrimitives.ReadInt32LittleEndian(payload) != 0 ? 1 : 0];
                }

                return property.Values.Length == 1 ? property : Bad(diagnostics, id, primitive);
            case ThemePrimitive.Color:
                if (resourceText is not null)
                {
                    property.Values = MsStylesValueParser.Integers(resourceText, 3);
                }
                else if (payload.Length >= 4)
                {
                    var color = MsStylesColor.FromColorRef(BinaryPrimitives.ReadInt32LittleEndian(payload));
                    property.Values = [color.R, color.G, color.B];
                }

                return property.Values.Length == 3 ? property : Bad(diagnostics, id, primitive);
            case ThemePrimitive.Margins:
            case ThemePrimitive.Rect:
                property.Values = resourceText is not null ? MsStylesValueParser.Integers(resourceText, 4) : Ints(payload, 4);
                return property.Values.Length == 4 ? property : Bad(diagnostics, id, primitive);
            case ThemePrimitive.Position:
                property.Values = resourceText is not null ? MsStylesValueParser.Integers(resourceText, 2) : Ints(payload, 2);
                return property.Values.Length == 2 ? property : Bad(diagnostics, id, primitive);
            case ThemePrimitive.IntList:
                if (resourceText is not null)
                {
                    property.Values = MsStylesValueParser.Integers(resourceText, MsStylesValueParser.MaxIntList);
                }
                else if (payload.Length >= 4)
                {
                    var count = BinaryPrimitives.ReadInt32LittleEndian(payload);
                    if (count < 0 || 4 + count * 4L > payload.Length)
                    {
                        return Bad(diagnostics, id, primitive);
                    }

                    property.Values = Ints(payload[4..], count);
                }

                return property;
            case ThemePrimitive.String:
                property.Text = resourceText ?? Encoding.Unicode.GetString(payload).TrimEnd('\0');
                return property;
            case ThemePrimitive.Font:
                if (resourceText is not null)
                {
                    if (!MsStylesValueParser.TryParseFont(resourceText, out var font))
                    {
                        return Bad(diagnostics, id, primitive);
                    }

                    property.Font = font;
                    property.Text = resourceText;
                }
                else if (TryLogFont(payload, out var logFont))
                {
                    property.Font = logFont;
                }
                else
                {
                    return Bad(diagnostics, id, primitive);
                }

                property.HasFont = true;
                return property;
            case ThemePrimitive.Filename:
            case ThemePrimitive.DiskStream:
                var type = primitive == ThemePrimitive.Filename ? "IMAGE" : "STREAM";
                property.Text = $"{type}/{resourceId}";
                if (resourceId != 0 && theme.TryImage(type, resourceId, out var image))
                {
                    property.Image = image;
                    property.HasImage = true;
                }
                else
                {
                    diagnostics.Add("image", property.Text);
                }

                return property;
            default:
                diagnostics.Add("primitive", ((int)primitive).ToString(System.Globalization.CultureInfo.InvariantCulture));
                return null;
        }
    }

    private static MsStylesProperty? Bad(MsStylesDiagnostics diagnostics, int id, ThemePrimitive primitive)
    {
        diagnostics.Add("value", $"{(ThemeProperty)id}:{primitive}");
        return null;
    }

    private static int[] Ints(ReadOnlySpan<byte> payload, int count)
    {
        if (payload.Length < count * 4)
        {
            return [];
        }

        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = BinaryPrimitives.ReadInt32LittleEndian(payload[(i * 4)..]);
        }

        return values;
    }

    private static bool TryLogFont(ReadOnlySpan<byte> payload, out MsStylesFont font)
    {
        font = default;
        if (payload.Length < 92)
        {
            return false;
        }

        var height = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var weight = BinaryPrimitives.ReadInt32LittleEndian(payload[16..]);
        var face = Encoding.Unicode.GetString(payload.Slice(28, 64));
        var end = face.IndexOf('\0');
        if (end >= 0)
        {
            face = face[..end];
        }

        var points = (int)Math.Round(Math.Abs(height) * 72.0 / 96.0);
        font = new MsStylesFont(face, points, weight == 0 ? MsStylesFont.Regular : weight, payload[20] != 0, payload[21] != 0, payload[22] != 0);
        return face.Length > 0;
    }
}
