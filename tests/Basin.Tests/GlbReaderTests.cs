using System.Buffers.Binary;
using System.Text;
using Basin.Scene3D.Gltf;
using Xunit;

namespace Basin.Tests;

public sealed class GlbReaderTests
{
    [Fact]
    public void A_triangle_flattens_into_one_mesh()
    {
        var document = GlbDocument.Parse(Glb(mode: 4, extensions: null));
        var mesh = GlbMeshes.Flatten(document, [], null);
        Assert.Equal(3, mesh.Vertices.Length);
        Assert.Equal([0u, 1u, 2u], mesh.Indices);
    }

    [Fact]
    public void Lines_are_refused_by_name()
    {
        var document = GlbDocument.Parse(Glb(mode: 1, extensions: null));
        var error = Assert.Throws<NotSupportedException>(() => GlbMeshes.Flatten(document, [], null));
        Assert.Contains("not triangles", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_required_extension_is_refused_by_name()
    {
        var error = Assert.Throws<NotSupportedException>(() => GlbDocument.Parse(Glb(mode: 4, extensions: "KHR_draco_mesh_compression")));
        Assert.Contains("KHR_draco_mesh_compression", error.Message, StringComparison.Ordinal);
    }

    private static byte[] Glb(int mode, string? extensions)
    {
        var binary = new byte[36];
        float[] positions = [0, 0, 0, 1, 0, 0, 0, 1, 0];
        for (var i = 0; i < positions.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(binary.AsSpan(i * 4), positions[i]);
        }

        var required = extensions is null ? string.Empty : $",\"extensionsRequired\":[\"{extensions}\"]";
        var json = "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}]," +
            $"\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":0}},\"mode\":{mode}}}]}}]," +
            "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\"}]," +
            "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":36}],\"buffers\":[{\"byteLength\":36}]" +
            required + "}";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var jsonLength = (jsonBytes.Length + 3) & ~3;
        var total = 12 + 8 + jsonLength + 8 + binary.Length;
        var glb = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(glb, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8), (uint)total);
        BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(12), jsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16), 0x4E4F534A);
        glb.AsSpan(20, jsonLength).Fill((byte)' ');
        jsonBytes.CopyTo(glb, 20);
        var binaryAt = 20 + jsonLength;
        BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(binaryAt), binary.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binaryAt + 4), 0x004E4942);
        binary.CopyTo(glb, binaryAt + 8);
        return glb;
    }
}
