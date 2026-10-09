using Xunit;

namespace LunaWm.Tests;

public sealed class JustTypeTests
{
    private static bool Starts(string line, string prefix) => line.StartsWith(prefix, StringComparison.Ordinal);

    private static LauncherEntry Entry(string id, string name, string? generic = null, params string[] keywords) =>
        new(id, name, null, [], generic, keywords);

    [Fact]
    public void Apps_match_on_name_generic_name_and_keywords_with_prefixes_first()
    {
        LauncherEntry[] entries =
        [
            Entry("a.desktop", "Terminal", "Console"),
            Entry("b.desktop", "Xterm Terminal"),
            Entry("c.desktop", "Files", null, "folder", "terminal"),
            Entry("d.desktop", "Clock"),
        ];
        var ranked = LunaShell.MatchApps(entries, "term").OrderBy(match => match.Rank).Select(match => match.Entry.Id).ToList();
        Assert.Equal(["a.desktop", "b.desktop", "c.desktop"], ranked);
        Assert.Single(LunaShell.MatchApps(entries, "conso"));
        Assert.Empty(LunaShell.MatchApps(entries, "zzz"));
    }

    [Theory]
    [InlineData("https://duckduckgo.com/?q=%s", "a b&c", "https://duckduckgo.com/?q=a%20b%26c")]
    [InlineData("https://example.com/search?q=", "x", "https://example.com/search?q=x")]
    public void The_search_address_encodes_the_text(string template, string query, string expected) =>
        Assert.Equal(expected, LunaShell.SearchAddress(template, query));

    [Fact]
    public async Task A_typed_key_in_card_view_opens_just_type_and_its_groups_lead_somewhere()
    {
        var ssdwin = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "wlclients", "bin", "ssdwin");
        Assert.SkipUnless(File.Exists(ssdwin), "ssdwin has not been built");
        using var config = new TempConfig();
        var applications = Path.Combine(config.Directory, "data", "applications");
        Directory.CreateDirectory(applications);
        File.WriteAllText(Path.Combine(applications, "sketch.desktop"), $"[Desktop Entry]\nType=Application\nName=Sketchbook\nGenericName=Drawing\nExec={ssdwin}\n");
        using var session = new LunaSession(LunaSession.Compositor(), config.Path, config.Environment, "-c", ssdwin);
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "CARD + 1 ")));
        await session.SendAsync("card-view");
        Assert.NotNull(await session.WaitForAsync(line => line == "CARDVIEW on"));
        await session.SendAsync("key 31 1");
        await session.SendAsync("key 31 0");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        await session.SendAsync("key 31 1");
        await session.SendAsync("key 31 0");
        Assert.NotNull(await session.WaitForAsync(line => line == "JUSTTYPE on s"));

        await session.SendAsync("type s");
        var rows = await session.WaitForAsync(line => Starts(line, "JUSTTYPE rows="));
        Assert.Equal("JUSTTYPE rows=[Apps],Sketchbook,[Cards],ssdwin,[Actions],Run \"s\",Search the web for \"s\"", rows);
        await session.SendAsync("just-type-choose 1");
        Assert.NotNull(await session.WaitForAsync(line => line == "JUSTTYPE chose card 1"));
        Assert.NotNull(await session.WaitForAsync(line => line == "CARDVIEW off 1"));

        await session.SendAsync("type sketch");
        await session.SendAsync("just-type-choose 0");
        Assert.NotNull(await session.WaitForAsync(line => line == "JUSTTYPE chose app sketch.desktop"));
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "LAUNCH Sketchbook pid=")));
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "CARD + 2 ")));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }
}
