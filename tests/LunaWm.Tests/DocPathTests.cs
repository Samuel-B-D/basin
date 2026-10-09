using Xunit;

namespace LunaWm.Tests;

public sealed class DocPathTests
{
    [Theory]
    [InlineData("X-DocPath=dolphin/index.html", "help:/dolphin/index.html")]
    [InlineData("X-GNOME-DocPath=mate-terminal/mate-terminal.xml", "help:mate-terminal")]
    [InlineData("X-DocPath=https://docs.example.org/app", "https://docs.example.org/app")]
    [InlineData("X-GNOME-DocPath=help:gedit", "help:gedit")]
    [InlineData("# X-DocPath=kcachegrind/index.html", null)]
    [InlineData("X-DocPath=", null)]
    public void A_documentation_key_becomes_a_help_address(string line, string? expected) =>
        Assert.Equal(expected, DocPath.UriFrom(["[Desktop Entry]", "Name=App", line]));

    [Fact]
    public void A_key_outside_the_entry_group_is_ignored() =>
        Assert.Null(DocPath.UriFrom(["[Desktop Entry]", "Name=App", "[Desktop Action Help]", "X-DocPath=app/index.html"]));
}
