using System.Text.Json.Serialization;

namespace LunaWm;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = true)]
[JsonSerializable(typeof(LauncherFile))]
internal sealed partial class LauncherJsonContext : JsonSerializerContext
{
}
