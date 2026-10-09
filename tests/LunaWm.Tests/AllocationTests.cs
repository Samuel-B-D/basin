using Xunit;

namespace LunaWm.Tests;

public sealed class AllocationTests
{
    private static string WlClient(string name)
    {
        var root = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && root is not null; i++)
        {
            var candidate = Path.Combine(root, "scripts", "wlclients", "bin", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar));
        }

        Assert.Skip($"scripts/wlclients/bin/{name} has not been built");
        return null!;
    }

    [Theory]
    [InlineData("pixman")]
    [InlineData("gl")]
    [InlineData("vulkan")]
    public async Task The_card_view_drag_and_the_stack_fling_allocate_nothing_per_frame(string renderer)
    {
        var ssdwin = WlClient("ssdwin");
        using var config = new TempConfig();
        using var session = await GoldenTests.Start(renderer, config);
        for (var i = 1; i <= 3; i++)
        {
            await session.SendAsync($"launch {ssdwin}");
            Assert.NotNull(await session.WaitForAsync(line => line.StartsWith($"CARD + {i} ", StringComparison.Ordinal)));
        }

        await session.SendAsync("alloc-probe drag 1000");
        Assert.Equal("ALLOCPROBE drag frames=1000 bytes=0", await session.WaitForAsync(line => line.StartsWith("ALLOCPROBE drag ", StringComparison.Ordinal)));
        await session.SendAsync("alloc-probe fling 1000");
        var fling = await session.WaitForAsync(line => line.StartsWith("ALLOCPROBE fling ", StringComparison.Ordinal));
        Assert.EndsWith(" bytes=0", fling!, StringComparison.Ordinal);
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("FRAMES ", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("pixman")]
    [InlineData("gl")]
    [InlineData("vulkan")]
    public async Task A_keyboard_press_and_a_held_backspace_allocate_nothing_per_frame(string renderer)
    {
        var textinput = WlClient("textinput");
        using var config = new TempConfig(KeyboardTests.Config);
        using var session = await GoldenTests.Start(renderer, config, "-c", $"{textinput} normal");
        Assert.NotNull(await session.WaitForAsync(line => line == "ENTER"));
        await session.SendAsync("touch 0.5 0.3");
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("KEYBOARD on ", StringComparison.Ordinal)));
        await session.SettleAsync();
        await session.SendAsync("alloc-probe keypress 1000");
        Assert.Equal("ALLOCPROBE keypress frames=1000 bytes=0", await session.WaitForAsync(line => line.StartsWith("ALLOCPROBE keypress ", StringComparison.Ordinal)));
        await session.SendAsync("alloc-probe repeat 1000");
        Assert.Equal("ALLOCPROBE repeat frames=1000 bytes=0", await session.WaitForAsync(line => line.StartsWith("ALLOCPROBE repeat ", StringComparison.Ordinal)));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("FRAMES ", StringComparison.Ordinal)));
    }
}
