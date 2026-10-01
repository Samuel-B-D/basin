using System.Buffers.Binary;
using System.Text;
using Basin.Themes.MsStyles;

namespace Basin.Tests;

internal sealed class PeImageWriter
{
    public const int ResourceFileOffset = 0x200;
    public const int ResourceRva = 0x1000;

    private readonly List<(PeResourceKey Type, PeResourceKey Name, byte[] Data)> _resources = [];

    public PeImageWriter Add(PeResourceKey type, PeResourceKey name, byte[] data)
    {
        _resources.Add((type, name, data));
        return this;
    }

    public PeImageWriter AddStrings(IReadOnlyDictionary<int, string> strings)
    {
        foreach (var block in strings.GroupBy(pair => pair.Key / 16))
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            for (var i = 0; i < 16; i++)
            {
                var id = block.Key * 16 + i;
                if (strings.TryGetValue(id, out var text))
                {
                    writer.Write((ushort)text.Length);
                    writer.Write(Encoding.Unicode.GetBytes(text));
                }
                else
                {
                    writer.Write((ushort)0);
                }
            }

            Add(PeResources.StringType, block.Key + 1, stream.ToArray());
        }

        return this;
    }

    public byte[] Build()
    {
        var section = BuildResourceSection();
        var rawSize = Align(section.Length, 0x200);
        var image = new byte[ResourceFileOffset + rawSize];
        var span = image.AsSpan();

        span[0] = (byte)'M';
        span[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(span[0x3c..], 0x40);
        var pe = 0x40;
        span[pe] = (byte)'P';
        span[pe + 1] = (byte)'E';
        var coff = pe + 4;
        BinaryPrimitives.WriteUInt16LittleEndian(span[coff..], 0x14c);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(coff + 2)..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(coff + 16)..], 224);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(coff + 18)..], 0x2102);

        var optional = coff + 20;
        BinaryPrimitives.WriteUInt16LittleEndian(span[optional..], 0x10b);
        BinaryPrimitives.WriteInt32LittleEndian(span[(optional + 28)..], 0x1000_0000);
        BinaryPrimitives.WriteInt32LittleEndian(span[(optional + 32)..], 0x1000);
        BinaryPrimitives.WriteInt32LittleEndian(span[(optional + 36)..], 0x200);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(optional + 40)..], 4);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(optional + 48)..], 4);
        BinaryPrimitives.WriteInt32LittleEndian(span[(optional + 56)..], ResourceRva + Align(section.Length, 0x1000));
        BinaryPrimitives.WriteInt32LittleEndian(span[(optional + 60)..], 0x200);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(optional + 68)..], 2);
        BinaryPrimitives.WriteInt32LittleEndian(span[(optional + 92)..], 16);
        var directories = optional + 96;
        BinaryPrimitives.WriteInt32LittleEndian(span[(directories + 16)..], ResourceRva);
        BinaryPrimitives.WriteInt32LittleEndian(span[(directories + 20)..], section.Length);

        var header = optional + 224;
        Encoding.ASCII.GetBytes(".rsrc").CopyTo(span[header..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[(header + 8)..], section.Length);
        BinaryPrimitives.WriteInt32LittleEndian(span[(header + 12)..], ResourceRva);
        BinaryPrimitives.WriteInt32LittleEndian(span[(header + 16)..], rawSize);
        BinaryPrimitives.WriteInt32LittleEndian(span[(header + 20)..], ResourceFileOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(header + 36)..], 0x4000_0040);

        section.CopyTo(span[ResourceFileOffset..]);
        return image;
    }

    private byte[] BuildResourceSection()
    {
        var types = _resources
            .GroupBy(r => r.Type)
            .Select(g => (Key: g.Key, Names: g.GroupBy(r => r.Name).Select(n => (Key: n.Key, Data: n.First().Data)).OrderBy(n => n.Key, KeyOrder.Instance).ToList()))
            .OrderBy(t => t.Key, KeyOrder.Instance)
            .ToList();

        var offset = 0;
        var rootOffset = offset;
        offset += 16 + types.Count * 8;
        var typeOffsets = new int[types.Count];
        var nameOffsets = new int[types.Count][];
        for (var t = 0; t < types.Count; t++)
        {
            typeOffsets[t] = offset;
            offset += 16 + types[t].Names.Count * 8;
        }

        for (var t = 0; t < types.Count; t++)
        {
            nameOffsets[t] = new int[types[t].Names.Count];
            for (var n = 0; n < types[t].Names.Count; n++)
            {
                nameOffsets[t][n] = offset;
                offset += 16 + 8;
            }
        }

        var strings = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in types.Select(t => t.Key).Concat(types.SelectMany(t => t.Names.Select(n => n.Key))))
        {
            if (key.Name is { } name && !strings.ContainsKey(name))
            {
                strings[name] = offset;
                offset += 2 + name.Length * 2;
            }
        }

        offset = Align(offset, 4);
        var dataEntryOffsets = new int[types.Count][];
        for (var t = 0; t < types.Count; t++)
        {
            dataEntryOffsets[t] = new int[types[t].Names.Count];
            for (var n = 0; n < types[t].Names.Count; n++)
            {
                dataEntryOffsets[t][n] = offset;
                offset += 16;
            }
        }

        var dataOffsets = new int[types.Count][];
        for (var t = 0; t < types.Count; t++)
        {
            dataOffsets[t] = new int[types[t].Names.Count];
            for (var n = 0; n < types[t].Names.Count; n++)
            {
                offset = Align(offset, 8);
                dataOffsets[t][n] = offset;
                offset += types[t].Names[n].Data.Length;
            }
        }

        var section = new byte[Align(offset, 8)];
        var span = section.AsSpan();
        WriteDirectory(span, rootOffset, types.Select(t => t.Key).ToList(), typeOffsets, true, strings);
        for (var t = 0; t < types.Count; t++)
        {
            WriteDirectory(span, typeOffsets[t], types[t].Names.Select(n => n.Key).ToList(), nameOffsets[t], true, strings);
            for (var n = 0; n < types[t].Names.Count; n++)
            {
                WriteDirectory(span, nameOffsets[t][n], [new PeResourceKey(1033)], [dataEntryOffsets[t][n]], false, strings);
                var entry = dataEntryOffsets[t][n];
                BinaryPrimitives.WriteInt32LittleEndian(span[entry..], ResourceRva + dataOffsets[t][n]);
                BinaryPrimitives.WriteInt32LittleEndian(span[(entry + 4)..], types[t].Names[n].Data.Length);
                types[t].Names[n].Data.CopyTo(span[dataOffsets[t][n]..]);
            }
        }

        foreach (var (name, at) in strings)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[at..], (ushort)name.Length);
            Encoding.Unicode.GetBytes(name).CopyTo(span[(at + 2)..]);
        }

        return section;
    }

    private static void WriteDirectory(Span<byte> span, int at, List<PeResourceKey> keys, IReadOnlyList<int> targets, bool subdirectories, Dictionary<string, int> strings)
    {
        var named = keys.Count(k => k.IsName);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(at + 12)..], (ushort)named);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(at + 14)..], (ushort)(keys.Count - named));
        for (var i = 0; i < keys.Count; i++)
        {
            var entry = at + 16 + i * 8;
            var name = keys[i].Name is { } text ? 0x8000_0000u | (uint)strings[text] : (uint)keys[i].Id;
            var target = (uint)targets[i] | (subdirectories ? 0x8000_0000u : 0);
            BinaryPrimitives.WriteUInt32LittleEndian(span[entry..], name);
            BinaryPrimitives.WriteUInt32LittleEndian(span[(entry + 4)..], target);
        }
    }

    private static int Align(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

    private sealed class KeyOrder : IComparer<PeResourceKey>
    {
        public static readonly KeyOrder Instance = new();

        public int Compare(PeResourceKey x, PeResourceKey y)
        {
            if (x.IsName != y.IsName)
            {
                return x.IsName ? -1 : 1;
            }

            return x.IsName
                ? string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase)
                : x.Id.CompareTo(y.Id);
        }
    }
}
