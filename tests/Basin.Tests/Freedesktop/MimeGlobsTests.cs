using Basin.Freedesktop;
using Xunit;

namespace Basin.Tests;

public sealed class MimeGlobsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("basin-mime-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Data(string name, string globs, string? icons = null, string? generic = null, string? aliases = null)
    {
        var mime = Path.Combine(_root, name, "mime");
        Directory.CreateDirectory(mime);
        File.WriteAllText(Path.Combine(mime, "globs2"), globs);
        if (icons is not null)
        {
            File.WriteAllText(Path.Combine(mime, "icons"), icons);
        }

        if (generic is not null)
        {
            File.WriteAllText(Path.Combine(mime, "generic-icons"), generic);
        }

        if (aliases is not null)
        {
            File.WriteAllText(Path.Combine(mime, "aliases"), aliases);
        }

        return Path.Combine(_root, name);
    }

    [Fact]
    public void Literals_beat_suffixes_and_the_longest_suffix_wins()
    {
        var data = Data("a", "50:text/x-makefile:Makefile\n50:application/gzip:*.gz\n50:application/x-compressed-tar:*.tar.gz\n50:text/plain:*.txt\n");
        var globs = new MimeGlobs([data]);

        Assert.Equal("text/x-makefile", globs.TypeOfName("Makefile"));
        Assert.Equal("application/x-compressed-tar", globs.TypeOfName("backup.tar.gz"));
        Assert.Equal("application/gzip", globs.TypeOfName("log.gz"));
        Assert.Equal("text/plain", globs.TypeOfName("README.TXT"));
        Assert.Null(globs.TypeOfName("nothing"));
    }

    [Fact]
    public void Weight_beats_length_and_case_sensitive_globs_keep_case()
    {
        var data = Data("a", "40:text/x-c:*.c:cs\n60:text/x-c++src:*.C:cs\n80:image/x-special:*.PNGish\n");
        var globs = new MimeGlobs([data]);

        Assert.Equal("text/x-c", globs.TypeOfName("main.c"));
        Assert.Equal("text/x-c++src", globs.TypeOfName("main.C"));
        Assert.Equal("image/x-special", globs.TypeOfName("a.pngish"));
    }

    [Fact]
    public void Full_patterns_match_when_nothing_simpler_does()
    {
        var data = Data("a", "50:text/x-readme:README*\n50:text/x-log:*.[0-9]\n");
        var globs = new MimeGlobs([data]);

        Assert.Equal("text/x-readme", globs.TypeOfName("README.md"));
        Assert.Equal("text/x-log", globs.TypeOfName("syslog.3"));
    }

    [Fact]
    public void Noglobs_in_a_more_important_directory_drops_the_less_important_globs()
    {
        var user = Data("user", "50:text/plain:__NOGLOBS__\n");
        var system = Data("system", "50:text/plain:*.txt\n");
        var globs = new MimeGlobs([user, system]);

        Assert.Null(globs.TypeOfName("a.txt"));
    }

    [Fact]
    public void Icons_fall_back_from_the_type_through_generic_to_the_media_generic()
    {
        var data = Data("a", "50:text/plain:*.txt\n", icons: "application/x-thing:thing-icon\n", generic: "text/plain:text-x-generic\n", aliases: "text/x-plain text/plain\n");
        var globs = new MimeGlobs([data]);

        Assert.Equal(["text-plain", "text-x-generic"], globs.IconNamesOf("text/plain"));
        Assert.Equal(["text-plain", "text-x-generic"], globs.IconNamesOf("text/x-plain"));
        Assert.Equal(["thing-icon", "application-x-generic"], globs.IconNamesOf("application/x-thing"));
    }

    [Fact]
    public void The_system_database_types_common_files()
    {
        Assert.SkipUnless(File.Exists("/usr/share/mime/globs2"), "shared-mime-info is not installed");
        var globs = new MimeGlobs(["/usr/share"]);

        Assert.Equal("text/plain", globs.TypeOfName("notes.txt"));
        Assert.Equal("image/png", globs.TypeOfName("photo.PNG"));
        Assert.Equal(MimeGlobs.Directory, globs.TypeOf("/usr"));
        Assert.Equal("Plain text document", globs.CommentOf("text/plain"));
    }
}
