using Basin.Backend.Wayland;

namespace TinyComp;

internal sealed class FrameHostChromeBinding(WaylandOutput output, FrameHostChrome chrome) : IDisposable
{
    public void Dispose()
    {
        output.Committed -= chrome.OnOutputChanged;
        chrome.Dispose();
    }
}
