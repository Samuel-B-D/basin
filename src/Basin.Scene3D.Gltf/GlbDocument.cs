using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;

namespace Basin.Scene3D.Gltf;

public sealed class GlbDocument
{
    private const uint Magic = 0x46546C67;
    private const uint JsonChunk = 0x4E4F534A;
    private const uint BinaryChunk = 0x004E4942;

    private readonly byte[] _data;
    private readonly int _binaryOffset;
    private readonly int _binaryLength;

    private GlbDocument(GltfRoot root, byte[] data, int binaryOffset, int binaryLength)
    {
        Root = root;
        _data = data;
        _binaryOffset = binaryOffset;
        _binaryLength = binaryLength;
    }

    public GltfRoot Root { get; }

    public GltfNode[] Nodes => Root.Nodes ?? [];

    public static GlbDocument Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var span = data.AsSpan();
        if (span.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(span) != Magic)
        {
            throw new InvalidDataException("Not a GLB file.");
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(span[4..]) != 2)
        {
            throw new InvalidDataException("Only glTF 2.0 is read.");
        }

        GltfRoot? root = null;
        var binaryOffset = 0;
        var binaryLength = 0;
        var offset = 12;
        while (offset + 8 <= span.Length)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]);
            var type = BinaryPrimitives.ReadUInt32LittleEndian(span[(offset + 4)..]);
            var chunk = span.Slice(offset + 8, length);
            if (type == JsonChunk)
            {
                root = JsonSerializer.Deserialize(chunk, GltfJsonContext.Default.GltfRoot);
            }
            else if (type == BinaryChunk)
            {
                binaryOffset = offset + 8;
                binaryLength = length;
            }

            offset += 8 + length;
        }

        if (root is null)
        {
            throw new InvalidDataException("GLB has no JSON chunk.");
        }

        if (root.ExtensionsRequired is { Length: > 0 } required)
        {
            throw new NotSupportedException($"GLB requires extensions: {string.Join(", ", required)}.");
        }

        return new GlbDocument(root, data, binaryOffset, binaryLength);
    }

    public int ComponentsOf(int accessor) => Components(Accessor(accessor).Type);

    public int CountOf(int accessor) => Accessor(accessor).Count;

    public float[] ReadFloats(int accessor)
    {
        var info = Accessor(accessor);
        var components = Components(info.Type);
        var result = new float[info.Count * components];
        var bytes = Source(info, components, out var stride);
        var size = ComponentSize(info.ComponentType);
        for (var i = 0; i < info.Count; i++)
        {
            var element = bytes[(i * stride)..];
            for (var c = 0; c < components; c++)
            {
                var at = element[(c * size)..];
                result[(i * components) + c] = info.ComponentType switch
                {
                    5126 => BinaryPrimitives.ReadSingleLittleEndian(at),
                    5121 => info.Normalized ? at[0] / 255f : at[0],
                    5123 => info.Normalized
                        ? BinaryPrimitives.ReadUInt16LittleEndian(at) / 65535f
                        : BinaryPrimitives.ReadUInt16LittleEndian(at),
                    5120 => info.Normalized ? Math.Max((sbyte)at[0] / 127f, -1f) : (sbyte)at[0],
                    5122 => info.Normalized
                        ? Math.Max(BinaryPrimitives.ReadInt16LittleEndian(at) / 32767f, -1f)
                        : BinaryPrimitives.ReadInt16LittleEndian(at),
                    _ => throw new NotSupportedException($"Component type {info.ComponentType} is not read as float."),
                };
            }
        }

        return result;
    }

    public uint[] ReadIntegers(int accessor)
    {
        var info = Accessor(accessor);
        var components = Components(info.Type);
        var result = new uint[info.Count * components];
        var bytes = Source(info, components, out var stride);
        var size = ComponentSize(info.ComponentType);
        for (var i = 0; i < info.Count; i++)
        {
            var element = bytes[(i * stride)..];
            for (var c = 0; c < components; c++)
            {
                var at = element[(c * size)..];
                result[(i * components) + c] = info.ComponentType switch
                {
                    5121 => at[0],
                    5123 => BinaryPrimitives.ReadUInt16LittleEndian(at),
                    5125 => BinaryPrimitives.ReadUInt32LittleEndian(at),
                    _ => throw new NotSupportedException($"Component type {info.ComponentType} is not an integer."),
                };
            }
        }

        return result;
    }

    public Matrix4x4[] ReadMatrices(int accessor)
    {
        var floats = ReadFloats(accessor);
        var result = new Matrix4x4[floats.Length / 16];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = ColumnMajor(floats.AsSpan(i * 16, 16));
        }

        return result;
    }

    public ReadOnlySpan<byte> ReadImage(int image)
    {
        var images = Root.Images ?? [];
        if ((uint)image >= (uint)images.Length || images[image].BufferView is not { } view)
        {
            throw new InvalidDataException($"Image {image} has no buffer view.");
        }

        return View(view);
    }

    public Matrix4x4 LocalMatrix(int node)
    {
        var info = Nodes[node];
        if (info.Matrix is { Length: 16 } matrix)
        {
            return ColumnMajor(matrix);
        }

        var translation = info.Translation is { Length: 3 } t ? new Vector3(t[0], t[1], t[2]) : Vector3.Zero;
        var rotation = info.Rotation is { Length: 4 } r ? new Quaternion(r[0], r[1], r[2], r[3]) : Quaternion.Identity;
        var scale = info.Scale is { Length: 3 } s ? new Vector3(s[0], s[1], s[2]) : Vector3.One;
        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
    }

    public int[] Parents()
    {
        var parents = new int[Nodes.Length];
        Array.Fill(parents, -1);
        for (var i = 0; i < Nodes.Length; i++)
        {
            foreach (var child in Nodes[i].Children ?? [])
            {
                parents[child] = i;
            }
        }

        return parents;
    }

    public Matrix4x4[] WorldMatrices()
    {
        var parents = Parents();
        var world = new Matrix4x4[Nodes.Length];
        var done = new bool[Nodes.Length];
        for (var i = 0; i < Nodes.Length; i++)
        {
            Resolve(i);
        }

        return world;

        void Resolve(int node)
        {
            if (done[node])
            {
                return;
            }

            var local = LocalMatrix(node);
            if (parents[node] is var parent and >= 0)
            {
                Resolve(parent);
                world[node] = local * world[parent];
            }
            else
            {
                world[node] = local;
            }

            done[node] = true;
        }
    }

    public int[] SceneRoots()
    {
        if (Root.Scenes is { Length: > 0 } scenes && scenes[Root.Scene ?? 0].Nodes is { } nodes)
        {
            return nodes;
        }

        var parents = Parents();
        var roots = new List<int>();
        for (var i = 0; i < parents.Length; i++)
        {
            if (parents[i] < 0)
            {
                roots.Add(i);
            }
        }

        return [.. roots];
    }

    private static Matrix4x4 ColumnMajor(ReadOnlySpan<float> m) => new(
        m[0], m[1], m[2], m[3],
        m[4], m[5], m[6], m[7],
        m[8], m[9], m[10], m[11],
        m[12], m[13], m[14], m[15]);

    private GltfAccessor Accessor(int accessor)
    {
        var accessors = Root.Accessors ?? [];
        if ((uint)accessor >= (uint)accessors.Length)
        {
            throw new InvalidDataException($"Accessor {accessor} does not exist.");
        }

        return accessors[accessor];
    }

    private ReadOnlySpan<byte> Source(GltfAccessor info, int components, out int stride)
    {
        if (info.BufferView is not { } viewIndex)
        {
            throw new NotSupportedException("Accessors without a buffer view are not read.");
        }

        var view = (Root.BufferViews ?? [])[viewIndex];
        var elementSize = ComponentSize(info.ComponentType) * components;
        stride = view.ByteStride is { } s and > 0 ? s : elementSize;
        return View(viewIndex)[info.ByteOffset..];
    }

    private ReadOnlySpan<byte> View(int viewIndex)
    {
        var view = (Root.BufferViews ?? [])[viewIndex];
        if (view.Buffer != 0 || view.ByteOffset + view.ByteLength > _binaryLength)
        {
            throw new InvalidDataException($"Buffer view {viewIndex} is outside the binary chunk.");
        }

        return _data.AsSpan(_binaryOffset + view.ByteOffset, view.ByteLength);
    }

    private static int Components(string type) => type switch
    {
        "SCALAR" => 1,
        "VEC2" => 2,
        "VEC3" => 3,
        "VEC4" => 4,
        "MAT4" => 16,
        _ => throw new NotSupportedException($"Accessor type {type} is not read."),
    };

    private static int ComponentSize(int componentType) => componentType switch
    {
        5120 or 5121 => 1,
        5122 or 5123 => 2,
        5125 or 5126 => 4,
        _ => throw new NotSupportedException($"Component type {componentType} is not read."),
    };
}
