using Basin.Diagnostics;
using Xunit;

namespace System7Wm.Tests;

public sealed class ApplicationDepthTests
{
    private static async Task<byte[]> ClientPixels(string renderer, bool apply, string depth)
    {
        var ssdwin = Prerequisites.WlClient("ssdwin");
        var config = Path.Combine(Path.GetTempPath(), $"system7-wm-appdepth-{Environment.ProcessId}-{apply}.toml");
        File.WriteAllText(config, $"[display]\napply_to_applications = {(apply ? "true" : "false")}\ndepth = \"{depth}\"\n");
        using var session = new Session(Prerequisites.Compositor(), renderer, null, "--config", config);
        var chosen = await session.WaitForPrefixAsync("RENDERER ");
        Assert.SkipWhen(chosen!.Split(' ')[1] != renderer, $"{renderer} fell back to {chosen.Split(' ')[1]}");
        var display = await session.DisplayAsync();
        using var client = session.StartClient(ssdwin, display, null, "server");
        Assert.NotNull(await session.WaitForPrefixAsync("WINDOW + "));
        await session.SettleAsync();
        var path = Path.Combine(session.Home, "shot.png");
        var mark = session.Mark();
        await session.SendAsync($"shotraw {path}");
        Assert.NotNull(await session.WaitForPrefixAsync($"SHOTRAW {path} ", mark));
        var (rgba, width, _) = PngCodec.Decode(File.ReadAllBytes(path));
        var pixels = new List<byte>();
        for (var y = 80; y < 280; y += 3)
        {
            for (var x = 20; x < 250; x += 3)
            {
                var i = ((y * width) + x) * 4;
                pixels.AddRange([rgba[i], rgba[i + 1], rgba[i + 2]]);
            }
        }

        await session.QuitCleanlyAsync();
        return [.. pixels];
    }

    [Theory]
    [InlineData("gl")]
    [InlineData("vulkan")]
    [InlineData("skia-gl")]
    public async Task Black_and_white_reaches_the_applications_when_asked(string renderer)
    {
        Prerequisites.SkipUnlessRenderer(renderer);
        var applied = await ClientPixels(renderer, apply: true, "bw");
        Assert.All(applied, channel => Assert.True(channel is 0 or 255, $"a client channel of {channel} under black and white"));
        Assert.Contains((byte)0, applied);
        Assert.Contains((byte)255, applied);

        var untouched = await ClientPixels(renderer, apply: false, "bw");
        Assert.Contains(untouched, channel => channel is not (0 or 255));
    }

    [Fact]
    public async Task A_renderer_without_shaders_reports_the_option_unavailable()
    {
        var config = Path.Combine(Path.GetTempPath(), $"system7-wm-appdepth-pixman-{Environment.ProcessId}.toml");
        File.WriteAllText(config, "[display]\napply_to_applications = true\ndepth = \"bw\"\n");
        using var session = new Session(Prerequisites.Compositor(), "pixman", null, "--config", config);
        Assert.NotNull(await session.WaitForAsync(line => line == "APPDEPTH unavailable on pixman"));
        await session.QuitCleanlyAsync();
    }
}
