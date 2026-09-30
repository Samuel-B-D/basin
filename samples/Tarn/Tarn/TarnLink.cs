using Basin;

namespace Tarn;

public static class TarnLink
{
    public static string DefaultEndpoint => PlatformFacts.HasDescriptors ? "127.0.0.1:9800" : "ws://127.0.0.1:9801";
}
