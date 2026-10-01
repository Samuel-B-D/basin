using Basin.Audio.PipeWire;
using Basin.Capabilities;
using Xunit;

namespace Basin.Tests;

public sealed class AudioStreamOwnersTests
{
    private sealed class FakeProcesses : IAudioProcessTable
    {
        public Dictionary<int, int> Parents { get; } = [];

        public Dictionary<int, int> Groups { get; } = [];

        public Dictionary<int, string> Flatpaks { get; } = [];

        public int ParentOf(int pid) => Parents.GetValueOrDefault(pid, 1);

        public int GroupOf(int pid) => Groups.GetValueOrDefault(pid, pid);

        public string? FlatpakAppOf(int pid) => Flatpaks.GetValueOrDefault(pid);
    }

    private static ulong Window(TestToplevelModel model, string app, uint pid)
    {
        var id = model.Add(app, app);
        model.SetIdentity(id, pid: pid);
        return id;
    }

    [Fact]
    public void A_launch_tag_names_the_window_in_that_process_group_when_no_pid_does()
    {
        var model = new TestToplevelModel();
        var processes = new FakeProcesses();
        Window(model, "other", 500);
        var tagged = Window(model, "tagged", 600);
        processes.Groups[600] = 590;
        processes.Parents[610] = 590;
        using var owners = new AudioStreamOwners(model, tag => tag == 7 ? 590 : 0, processes);

        var owner = owners.Match(new AudioStream(40) { LaunchId = 7, VerifiedPid = 610 });

        Assert.Equal(new AudioStreamOwner(tagged, AudioMatchStep.LaunchTag), owner);
        Assert.Equal("launch-tag", owner.ViaName);
    }

    [Fact]
    public void A_program_started_in_a_terminal_plays_from_its_own_window_not_the_terminal()
    {
        var model = new TestToplevelModel();
        var processes = new FakeProcesses();
        var foot = Window(model, "foot", 600);
        var mpv = Window(model, "mpv", 720);
        processes.Groups[600] = 600;
        processes.Groups[720] = 700;
        processes.Parents[720] = 700;
        processes.Parents[700] = 600;
        using var owners = new AudioStreamOwners(model, tag => tag == 7 ? 600 : 0, processes);

        Assert.Equal(new AudioStreamOwner(mpv, AudioMatchStep.SecPid), owners.Match(new AudioStream(40) { LaunchId = 7, VerifiedPid = 720 }));
        Assert.Equal(new AudioStreamOwner(mpv, AudioMatchStep.ReportedPid), owners.Match(new AudioStream(41) { LaunchId = 7, ViaPulse = true, VerifiedPid = 300, ReportedPid = 720 }));
        Assert.Equal(new AudioStreamOwner(foot, AudioMatchStep.Ancestor), owners.Match(new AudioStream(42) { LaunchId = 7, VerifiedPid = 700 }));
    }

    [Fact]
    public void A_native_stream_matches_its_verified_pid()
    {
        var model = new TestToplevelModel();
        Window(model, "a", 500);
        var b = Window(model, "b", 501);
        using var owners = new AudioStreamOwners(model, processes: new FakeProcesses());

        Assert.Equal(new AudioStreamOwner(b, AudioMatchStep.SecPid), owners.Match(new AudioStream(40) { VerifiedPid = 501 }));
    }

    [Fact]
    public void A_helper_process_matches_through_its_parent_chain()
    {
        var model = new TestToplevelModel();
        var browser = Window(model, "browser", 500);
        var processes = new FakeProcesses();
        processes.Parents[700] = 650;
        processes.Parents[650] = 500;
        using var owners = new AudioStreamOwners(model, processes: processes);

        Assert.Equal(new AudioStreamOwner(browser, AudioMatchStep.Ancestor), owners.Match(new AudioStream(40) { VerifiedPid = 700 }));
    }

    [Fact]
    public void The_ancestor_walk_stops_after_sixteen_steps()
    {
        var model = new TestToplevelModel();
        Window(model, "root", 100);
        var processes = new FakeProcesses();
        for (var pid = 200; pid > 101; pid--)
        {
            processes.Parents[pid] = pid - 1;
        }

        processes.Parents[101] = 100;
        using var owners = new AudioStreamOwners(model, processes: processes);

        Assert.False(owners.Match(new AudioStream(40) { VerifiedPid = 200 }).Matched);
        Assert.True(owners.Match(new AudioStream(41) { VerifiedPid = 110 }).Matched);
    }

    [Fact]
    public void A_flatpak_stream_matches_the_window_in_that_instance()
    {
        var model = new TestToplevelModel();
        Window(model, "host", 500);
        var sandboxed = Window(model, "sandboxed", 800);
        var processes = new FakeProcesses();
        processes.Flatpaks[800] = "org.example.Player";
        using var owners = new AudioStreamOwners(model, processes: processes);

        var owner = owners.Match(new AudioStream(40) { ViaPulse = true, VerifiedPid = 300, ReportedPid = 2, FlatpakAppId = "org.example.Player" });

        Assert.Equal(new AudioStreamOwner(sandboxed, AudioMatchStep.Flatpak), owner);
    }

    [Fact]
    public void A_flatpak_stream_never_falls_back_to_its_reported_pid()
    {
        var model = new TestToplevelModel();
        Window(model, "host", 2);
        using var owners = new AudioStreamOwners(model, processes: new FakeProcesses());

        var owner = owners.Match(new AudioStream(40) { ViaPulse = true, ReportedPid = 2, FlatpakAppId = "org.example.Player" });

        Assert.False(owner.Matched);
    }

    [Fact]
    public void A_pulse_stream_matches_its_reported_pid_and_never_the_pulse_server_pid()
    {
        var model = new TestToplevelModel();
        var pulse = Window(model, "pipewire-pulse", 300);
        var app = Window(model, "app", 900);
        var processes = new FakeProcesses();
        processes.Parents[950] = 900;
        using var owners = new AudioStreamOwners(model, processes: processes);

        Assert.Equal(new AudioStreamOwner(app, AudioMatchStep.ReportedPid), owners.Match(new AudioStream(40) { ViaPulse = true, VerifiedPid = 300, ReportedPid = 900 }));
        Assert.Equal(new AudioStreamOwner(app, AudioMatchStep.ReportedPid), owners.Match(new AudioStream(41) { ViaPulse = true, VerifiedPid = 300, ReportedPid = 950 }));
        Assert.NotEqual(pulse, owners.Match(new AudioStream(42) { ViaPulse = true, VerifiedPid = 300 }).ToplevelId);
    }

    [Fact]
    public void A_pid_that_owns_two_windows_goes_to_the_one_focused_last_else_the_first_mapped()
    {
        var model = new TestToplevelModel();
        var first = Window(model, "first", 500);
        var second = Window(model, "second", 500);
        using var owners = new AudioStreamOwners(model, processes: new FakeProcesses());
        var stream = new AudioStream(40) { VerifiedPid = 500 };

        Assert.Equal(first, owners.Match(stream).ToplevelId);

        model.SetState(second, ToplevelState.Activated);
        Assert.Equal(second, owners.Match(stream).ToplevelId);

        model.SetState(second, ToplevelState.None);
        model.SetState(first, ToplevelState.Activated);
        Assert.Equal(first, owners.Match(stream).ToplevelId);
    }

    [Fact]
    public void A_stream_seen_before_its_window_matches_once_the_window_maps()
    {
        var model = new TestToplevelModel();
        using var owners = new AudioStreamOwners(model, processes: new FakeProcesses());
        var stream = new AudioStream(40) { VerifiedPid = 500 };

        Assert.False(owners.Match(stream).Matched);

        var window = Window(model, "late", 500);
        Assert.Equal(new AudioStreamOwner(window, AudioMatchStep.SecPid), owners.Match(stream));
    }

    [Fact]
    public void Stream_properties_merge_the_node_over_its_client()
    {
        var node = new Dictionary<string, string?>
        {
            ["client.id"] = "31",
            ["object.serial"] = "812",
            ["client.api"] = "pipewire-pulse",
            ["application.name"] = "Firefox",
            ["media.role"] = "Music",
        };
        var client = new Dictionary<string, string?>
        {
            ["pipewire.sec.pid"] = "300",
            ["application.process.id"] = "900",
            ["pipewire.access.portal.app_id"] = "org.mozilla.firefox",
            ["basin.launch.id"] = "4",
            ["application.name"] = "ignored",
        };

        var stream = AudioStreams.Describe(55, node, client);

        Assert.Equal(55u, stream.NodeId);
        Assert.Equal(812ul, stream.Serial);
        Assert.Equal(31u, stream.ClientId);
        Assert.Equal(300, stream.VerifiedPid);
        Assert.Equal(900, stream.ReportedPid);
        Assert.Equal("org.mozilla.firefox", stream.FlatpakAppId);
        Assert.Equal("Firefox", stream.ApplicationName);
        Assert.Equal("Music", stream.MediaRole);
        Assert.True(stream.ViaPulse);
        Assert.Equal(4, stream.LaunchId);
    }
}
