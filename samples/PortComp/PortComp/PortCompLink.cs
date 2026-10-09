using Basin;

namespace PortComp;

public static class PortCompLink
{
    public static string DefaultEndpoint => PlatformFacts.HasDescriptors ? "127.0.0.1:9800" : "ws://127.0.0.1:9801";
}
