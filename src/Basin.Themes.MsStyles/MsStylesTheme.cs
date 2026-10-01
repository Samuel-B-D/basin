using System.Buffers.Binary;
using System.Globalization;

namespace Basin.Themes.MsStyles;

public sealed class MsStylesTheme
{
    private static readonly string[] KnownTypes =
    [
        "PACKTHEM_VERSION", "COLORNAMES", "SIZENAMES", "FILERESNAMES", "ORIGFILENAMES", "MINDEPTH", "COMBODATA",
        "TEXTFILE", "CMAP", "VMAP", "BCMAP", "RMAP", "AMAP", "VARIANT", "IMAGE", "STREAM", "MINCOLORDEPTH", "MUI",
    ];

    private static readonly int[] KnownIntegerTypes = [PeResources.BitmapType, PeResources.StringType, 16, 24];

    private readonly Dictionary<string, MsStylesClass> _classes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MsStylesClass> _classOrder = [];
    private readonly Dictionary<string, MsStylesImage> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _colorDisplayNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _sizeDisplayNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly PeResources? _mui;
    private MsStylesSysMetrics? _sysMetrics;

    private MsStylesTheme(PeResources resources, PeResources? mui, int format)
    {
        Resources = resources;
        _mui = mui;
        Format = format;
    }

    public int Format { get; }

    public PeResources Resources { get; }

    public bool HasMui => _mui is not null;

    public MsStylesDocumentation Documentation { get; private set; } = new();

    public IReadOnlyList<string> ColorSchemes { get; private set; } = [];

    public IReadOnlyList<string> Sizes { get; private set; } = [];

    public string Color { get; private set; } = string.Empty;

    public string Size { get; private set; } = string.Empty;

    public string? Path { get; private set; }

    public MsStylesDiagnostics Diagnostics { get; } = new();

    public IReadOnlyList<MsStylesClass> Classes => _classOrder;

    public IReadOnlyCollection<MsStylesImage> Images => _images.Values;

    public MsStylesSysMetrics SysMetrics => _sysMetrics ??= new MsStylesSysMetrics(FindClass(string.Empty, "sysmetrics"));

    public ReadOnlyMemory<byte> BaseClassMap { get; private set; }

    public ReadOnlyMemory<byte> ResourceMap { get; private set; }

    public ReadOnlyMemory<byte> AnimationMap { get; private set; }

    public static MsStylesTheme Load(string path, string? color = null, string? size = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        var bytes = File.ReadAllBytes(path);
        ReadOnlyMemory<byte> mui = default;
        if (FindMui(path) is { } muiPath)
        {
            mui = File.ReadAllBytes(muiPath);
        }

        var theme = Parse(bytes, color, size, mui);
        theme.Path = System.IO.Path.GetFullPath(path);
        return theme;
    }

    public static MsStylesTheme Parse(ReadOnlyMemory<byte> file, string? color = null, string? size = null, ReadOnlyMemory<byte> mui = default)
    {
        var resources = PeResources.Read(file);
        if (!resources.TryFind("PACKTHEM_VERSION", 1, out var versionBytes) || versionBytes.Length != 2)
        {
            throw new MsStylesFormatException("No PACKTHEM_VERSION resource: not a visual style.");
        }

        var format = BinaryPrimitives.ReadUInt16LittleEndian(versionBytes.Span);
        if (format is not (3 or 4))
        {
            throw new MsStylesFormatException($"PACKTHEM_VERSION {format} is not supported (3 and 4 are).");
        }

        PeResources? muiResources = null;
        if (!mui.IsEmpty)
        {
            muiResources = PeResources.Read(mui);
        }

        var theme = new MsStylesTheme(resources, muiResources, format);
        theme.CountUnknownTypes();
        if (format == 3)
        {
            MsStylesIniReader.Read(theme, color, size);
        }
        else
        {
            MsStylesVariantReader.Read(theme, color, size);
        }

        theme.Link();
        theme.Documentation = theme.ReadDocumentation();
        return theme;
    }

    public static string? FindMui(string path)
    {
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) ?? ".";
        var file = System.IO.Path.GetFileName(path) + ".mui";
        var culture = CultureInfo.CurrentUICulture.Name;
        foreach (var candidate in new[]
                 {
                     culture.Length > 0 ? System.IO.Path.Combine(directory, culture, file) : null,
                     System.IO.Path.Combine(directory, "en-US", file),
                     System.IO.Path.Combine(directory, file),
                 })
        {
            if (candidate is not null && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public string DisplayNameOfColor(string color) =>
        _colorDisplayNames.TryGetValue(color, out var name) ? name : color;

    public string DisplayNameOfSize(string size) =>
        _sizeDisplayNames.TryGetValue(size, out var name) ? name : size;

    public MsStylesClass? OpenClass(string classList, string? app = null) => OpenClass(classList.AsSpan(), app);

    public MsStylesClass? OpenClass(ReadOnlySpan<char> classList, string? app = null)
    {
        while (true)
        {
            var separator = classList.IndexOf(';');
            var entry = (separator < 0 ? classList : classList[..separator]).Trim();
            if (entry.Length > 0)
            {
                var appSeparator = entry.IndexOf("::", StringComparison.Ordinal);
                var entryApp = appSeparator >= 0 ? entry[..appSeparator] : ReadOnlySpan<char>.Empty;
                var entryClass = appSeparator >= 0 ? entry[(appSeparator + 2)..] : entry;
                if (entryApp.Length > 0 && app is not null)
                {
                    return null;
                }

                var wantedApp = entryApp.Length > 0 ? entryApp : app.AsSpan();
                var found = wantedApp.Length > 0 ? FindClass(wantedApp, entryClass) : null;
                found ??= FindClass(ReadOnlySpan<char>.Empty, entryClass);
                if (found is not null)
                {
                    return found;
                }
            }

            if (separator < 0)
            {
                return null;
            }

            classList = classList[(separator + 1)..];
        }
    }

    internal MsStylesClass? FindClass(ReadOnlySpan<char> app, ReadOnlySpan<char> name)
    {
        var lookup = _classes.GetAlternateLookup<ReadOnlySpan<char>>();
        if (app.Length == 0)
        {
            return lookup.TryGetValue(name, out var plain) ? plain : null;
        }

        Span<char> key = stackalloc char[app.Length + 2 + name.Length];
        app.CopyTo(key);
        key[app.Length] = ':';
        key[app.Length + 1] = ':';
        name.CopyTo(key[(app.Length + 2)..]);
        return lookup.TryGetValue(key, out var found) ? found : null;
    }

    internal MsStylesClass AddClass(string app, string name)
    {
        var key = app.Length == 0 ? name : app + "::" + name;
        if (!_classes.TryGetValue(key, out var cls))
        {
            cls = new MsStylesClass(this, app, name);
            _classes[key] = cls;
            _classOrder.Add(cls);
        }

        return cls;
    }

    internal void SetVariants(IReadOnlyList<string> colors, IReadOnlyList<string> sizes, string color, string size)
    {
        ColorSchemes = colors;
        Sizes = sizes;
        Color = color;
        Size = size;
    }

    internal void SetColorDisplayName(string color, string name) => _colorDisplayNames[color] = name;

    internal void SetSizeDisplayName(string size, string name) => _sizeDisplayNames[size] = name;

    internal void SetMaps(ReadOnlyMemory<byte> baseClassMap, ReadOnlyMemory<byte> resourceMap, ReadOnlyMemory<byte> animationMap)
    {
        BaseClassMap = baseClassMap;
        ResourceMap = resourceMap;
        AnimationMap = animationMap;
    }

    internal bool TryGetString(int id, out string value)
    {
        if (Resources.TryGetString(id, out value))
        {
            return true;
        }

        return _mui is not null && _mui.TryGetString(id, out value);
    }

    internal bool TryImage(PeResourceKey type, PeResourceKey name, out MsStylesImage image)
    {
        var key = type + "/" + name;
        if (_images.TryGetValue(key, out image))
        {
            return true;
        }

        if (!Resources.TryFind(type, name, out var bytes))
        {
            image = default;
            return false;
        }

        var format = DetectFormat(bytes.Span);
        if (format == MsStylesImageFormat.Dib && !(bytes.Length >= 2 && bytes.Span[0] == 'B' && bytes.Span[1] == 'M'))
        {
            bytes = WithFileHeader(bytes.Span);
        }

        var premultiplied = format == MsStylesImageFormat.Png && type == new PeResourceKey("IMAGE");
        image = new MsStylesImage(_images.Count + 1, name.ToString(), bytes, format, premultiplied);
        _images[key] = image;
        return true;
    }

    private static MsStylesImageFormat DetectFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == (byte)'P' && bytes[2] == (byte)'N' && bytes[3] == (byte)'G')
        {
            return MsStylesImageFormat.Png;
        }

        if (bytes.Length >= 2 && bytes[0] == 'B' && bytes[1] == 'M')
        {
            return MsStylesImageFormat.Dib;
        }

        if (bytes.Length >= 16)
        {
            var headerSize = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            if (headerSize is 12 or 40 or 52 or 56 or 108 or 124)
            {
                return MsStylesImageFormat.Dib;
            }
        }

        return MsStylesImageFormat.Unknown;
    }

    private static byte[] WithFileHeader(ReadOnlySpan<byte> dib)
    {
        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(dib);
        int bitCount;
        int colorsUsed = 0;
        int compression = 0;
        if (headerSize == 12)
        {
            bitCount = BinaryPrimitives.ReadUInt16LittleEndian(dib[10..]);
        }
        else
        {
            bitCount = BinaryPrimitives.ReadUInt16LittleEndian(dib[14..]);
            compression = BinaryPrimitives.ReadInt32LittleEndian(dib[16..]);
            colorsUsed = BinaryPrimitives.ReadInt32LittleEndian(dib[32..]);
        }

        var paletteEntries = colorsUsed != 0 ? colorsUsed : bitCount <= 8 ? 1 << bitCount : 0;
        var entrySize = headerSize == 12 ? 3 : 4;
        var masks = headerSize == 40 && compression is 3 or 6 ? (compression == 6 ? 16 : 12) : 0;
        var offBits = 14 + headerSize + masks + paletteEntries * entrySize;
        var file = new byte[14 + dib.Length];
        file[0] = (byte)'B';
        file[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(2), file.Length);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(10), Math.Min(offBits, file.Length));
        dib.CopyTo(file.AsSpan(14));
        return file;
    }

    private void CountUnknownTypes()
    {
        foreach (var type in Resources.Types)
        {
            if (type.IsName ? Array.Exists(KnownTypes, k => string.Equals(k, type.Name, StringComparison.OrdinalIgnoreCase)) : Array.IndexOf(KnownIntegerTypes, type.Id) >= 0)
            {
                continue;
            }

            Diagnostics.Add("resource-type", type.ToString());
        }
    }

    private void Link()
    {
        var globals = FindClass(ReadOnlySpan<char>.Empty, "globals");
        foreach (var cls in _classOrder)
        {
            if (cls.App.Length > 0)
            {
                cls.Next = FindClass(ReadOnlySpan<char>.Empty, cls.Name) ?? globals;
            }
            else if (!ReferenceEquals(cls, globals))
            {
                cls.Next = globals;
            }
        }
    }

    private MsStylesDocumentation ReadDocumentation()
    {
        var documentation = FindClass(ReadOnlySpan<char>.Empty, "documentation");
        string? Get(ThemeProperty id, int xpString)
        {
            if (documentation is not null && documentation.TryGetString(0, 0, id, out var value) && value.Length > 0)
            {
                return value;
            }

            return Format == 3 && TryGetString(xpString, out var text) ? text : null;
        }

        return new MsStylesDocumentation
        {
            Name = Get(ThemeProperty.Name, -1),
            DisplayName = Get(ThemeProperty.DisplayName, 5000),
            Tooltip = Get(ThemeProperty.Tooltip, 5001),
            Company = Get(ThemeProperty.Company, 5002),
            Author = Get(ThemeProperty.Author, 5003),
            Copyright = Get(ThemeProperty.Copyright, 5004),
            Url = Get(ThemeProperty.Url, 5005),
            Version = Get(ThemeProperty.Version, 5006),
            Description = Get(ThemeProperty.Description, 5007),
        };
    }
}
