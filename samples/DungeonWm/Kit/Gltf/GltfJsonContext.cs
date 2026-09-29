using System.Text.Json.Serialization;

namespace DungeonWm;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GltfRoot))]
internal sealed partial class GltfJsonContext : JsonSerializerContext
{
}
