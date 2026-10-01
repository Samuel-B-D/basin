namespace Basin.Themes.MsStyles;

public static partial class MsStylesStyleMap
{
    private static readonly Dictionary<string, MsStylesPropertyName> ByPropertyName;
    private static readonly Dictionary<int, MsStylesPropertyName> ByPropertyId;
    private static readonly Dictionary<string, MsStylesClassMap> ByClassName;

    static MsStylesStyleMap()
    {
        ByPropertyName = BuildPropertyIndex();
        ByPropertyId = BuildIdIndex();
        ByClassName = new Dictionary<string, MsStylesClassMap>(StringComparer.OrdinalIgnoreCase);
        foreach (var map in Classes)
        {
            ByClassName[map.Name] = map;
        }
    }

    public static IReadOnlyList<MsStylesPropertyName> Properties => PropertyNames;

    public static IReadOnlyList<MsStylesEnumName> Enums => EnumNames;

    public static IReadOnlyList<MsStylesClassMap> ClassMaps => Classes;

    public static bool TryLookupProperty(string name, out MsStylesPropertyName property)
    {
        if (ByPropertyName.TryGetValue(name, out property))
        {
            return true;
        }

        if (Enum.TryParse<ThemeProperty>(name, ignoreCase: true, out var id) && !int.TryParse(name, out _) &&
            PrimitiveOfRange((int)id) is var primitive && primitive != ThemePrimitive.None)
        {
            property = new MsStylesPropertyName(name.ToUpperInvariant(), primitive, id);
            return true;
        }

        return false;
    }

    public static ThemePrimitive PrimitiveOfRange(int id) => id switch
    {
        >= 600 and <= 608 => ThemePrimitive.String,
        >= 801 and <= 899 => ThemePrimitive.Font,
        >= 1201 and <= 1299 => ThemePrimitive.Size,
        >= 1601 and <= 1699 => ThemePrimitive.Color,
        >= 2201 and <= 2299 => ThemePrimitive.Bool,
        >= 2401 and <= 2499 => ThemePrimitive.Int,
        >= 2601 and <= 2699 => ThemePrimitive.Font,
        >= 3001 and <= 3099 => ThemePrimitive.Filename,
        >= 3201 and <= 3299 => ThemePrimitive.String,
        >= 3401 and <= 3499 => ThemePrimitive.Position,
        >= 3601 and <= 3699 => ThemePrimitive.Margins,
        >= 3801 and <= 3899 => ThemePrimitive.Color,
        >= 4001 and <= 4099 => ThemePrimitive.Enum,
        5001 => ThemePrimitive.Filename,
        5002 or 5004 or 5005 => ThemePrimitive.Rect,
        5003 => ThemePrimitive.Color,
        5006 => ThemePrimitive.Int,
        6000 => ThemePrimitive.IntList,
        8002 => ThemePrimitive.Rect,
        _ => ThemePrimitive.None,
    };

    public static bool TryLookupProperty(int id, out MsStylesPropertyName property) =>
        ByPropertyId.TryGetValue(id, out property);

    public static bool TryLookupEnum(ThemeProperty property, string name, out int value)
    {
        foreach (var entry in EnumNames)
        {
            if (entry.Property == property && string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = 0;
        return false;
    }

    public static string? EnumName(ThemeProperty property, int value)
    {
        foreach (var entry in EnumNames)
        {
            if (entry.Property == property && entry.Value == value)
            {
                return entry.Name;
            }
        }

        return null;
    }

    public static bool IsEnumProperty(ThemeProperty property)
    {
        foreach (var entry in EnumNames)
        {
            if (entry.Property == property)
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasClass(string className) => ByClassName.ContainsKey(className);

    public static bool TryLookupPartState(string className, string? part, string? state, out int partId, out int stateId)
    {
        partId = 0;
        stateId = 0;
        if (ByClassName.TryGetValue(className, out var map))
        {
            if (part is not null)
            {
                foreach (var entry in map.Entries)
                {
                    if (entry.State == 0 && string.Equals(entry.Name, part, StringComparison.OrdinalIgnoreCase))
                    {
                        partId = entry.Part;
                        break;
                    }
                }
            }

            if (state is not null && (part is null || partId != 0))
            {
                foreach (var entry in map.Entries)
                {
                    var partMatches = part is null ? true : entry.Part == partId;
                    if (partMatches && entry.State != 0 && string.Equals(entry.Name, state, StringComparison.OrdinalIgnoreCase))
                    {
                        stateId = entry.State;
                        break;
                    }
                }
            }
        }

        return (part is null || partId != 0) && (state is null || stateId != 0);
    }

    public static string? PartName(string className, int part)
    {
        if (part == 0 || !ByClassName.TryGetValue(BaseName(className), out var map))
        {
            return null;
        }

        foreach (var entry in map.Entries)
        {
            if (entry.Part == part && entry.State == 0)
            {
                return entry.Name;
            }
        }

        return null;
    }

    public static string? StateName(string className, int part, int state)
    {
        if (state == 0 || !ByClassName.TryGetValue(BaseName(className), out var map))
        {
            return null;
        }

        foreach (var entry in map.Entries)
        {
            if (entry.State == state && (part == 0 ? true : entry.Part == part))
            {
                return entry.Name;
            }
        }

        return null;
    }

    private static string BaseName(string className)
    {
        var separator = className.IndexOf("::", StringComparison.Ordinal);
        return separator >= 0 ? className[(separator + 2)..] : className;
    }

    private static Dictionary<string, MsStylesPropertyName> BuildPropertyIndex()
    {
        var index = new Dictionary<string, MsStylesPropertyName>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in PropertyNames)
        {
            index.TryAdd(entry.Name, entry);
        }

        return index;
    }

    private static Dictionary<int, MsStylesPropertyName> BuildIdIndex()
    {
        var index = new Dictionary<int, MsStylesPropertyName>();
        foreach (var entry in PropertyNames)
        {
            index.TryAdd((int)entry.Id, entry);
        }

        return index;
    }
}
