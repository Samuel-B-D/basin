using System.Text;
using Basin.Config;
using Basin.Diagnostics;
using Xunit;

namespace Basin.Tests;

public sealed class TomlConfigLoadTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("basin-toml-load-").FullName;
    private readonly string? _previous = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
    private static readonly BasinLogger Log = BasinLog.For("test");

    public TomlConfigLoadTests() => Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _home);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _previous);
        Directory.Delete(_home, recursive: true);
    }

    private static Stream Template() => new MemoryStream(Encoding.UTF8.GetBytes("[a]\nb = 1\n"));

    [Fact]
    public void The_default_file_is_seeded_once_and_then_read()
    {
        var load = TomlConfig.Load(null, "sample", none: false, Template, Log);
        var file = TomlConfig.DefaultPath("sample");

        Assert.Equal(file, load.File);
        Assert.Null(load.Fatal);
        Assert.NotNull(load.Table);
        Assert.Equal("[a]\nb = 1\n", File.ReadAllText(file));

        File.WriteAllText(file, "[a]\nb = 2\n");
        Assert.Equal(2L, ((Tomlyn.Model.TomlTable)TomlConfig.Load(null, "sample", none: false, Template, Log).Table!["a"])["b"]);
    }

    [Fact]
    public void A_broken_named_file_is_fatal_and_a_broken_default_file_is_not()
    {
        var named = Path.Combine(_home, "named.toml");
        File.WriteAllText(named, "[a\n");
        var fromNamed = TomlConfig.Load(named, "sample", none: false, Template, Log);
        Assert.Null(fromNamed.Table);
        Assert.StartsWith($"{named}: ", fromNamed.Fatal);

        var missing = TomlConfig.Load(Path.Combine(_home, "missing.toml"), "sample", none: false, Template, Log);
        Assert.NotNull(missing.Fatal);
        Assert.False(File.Exists(Path.Combine(_home, "missing.toml")));

        var file = TomlConfig.DefaultPath("sample");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "[a\n");
        var fromDefault = TomlConfig.Load(null, "sample", none: false, Template, Log);
        Assert.Null(fromDefault.Table);
        Assert.Null(fromDefault.Fatal);
        Assert.Equal("[a\n", File.ReadAllText(file));
    }

    [Fact]
    public void None_reads_and_seeds_nothing()
    {
        var load = TomlConfig.Load(null, "sample", none: true, Template, Log);

        Assert.Equal(default, load);
        Assert.False(File.Exists(TomlConfig.DefaultPath("sample")));
    }
}
