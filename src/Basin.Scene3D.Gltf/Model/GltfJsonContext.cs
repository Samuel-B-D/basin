using System.Text.Json.Serialization;

namespace Basin.Scene3D.Gltf;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GltfRoot))]
internal sealed partial class GltfJsonContext : JsonSerializerContext
{
}
