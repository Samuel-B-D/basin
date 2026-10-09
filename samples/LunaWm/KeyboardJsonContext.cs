using System.Text.Json.Serialization;

namespace LunaWm;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(KeyboardFile))]
internal sealed partial class KeyboardJsonContext : JsonSerializerContext
{
}
