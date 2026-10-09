using Xunit;

namespace LunaWm.Tests;

public sealed class KeyboardTests
{
    internal const string Config = TempConfig.Quiet + """

        [keyboard]
        layouts = ["us-qwerty"]
        sounds = false
        """;

    private static bool Starts(string line, string prefix) => line.StartsWith(prefix, StringComparison.Ordinal);

    internal static string WlClient(string name)
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

    private static async Task<LunaSession> Field(TempConfig config, string arguments)
    {
        var textinput = WlClient("textinput");
        var session = new LunaSession(LunaSession.Compositor(), config.Path, config.Environment, "-c", $"{textinput} {arguments}");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "CARD + 1 textinput")));
        Assert.NotNull(await session.WaitForAsync(line => line == "ENTER"));
        await session.SettleAsync();
        return session;
    }

    private static async Task Raise(LunaSession session)
    {
        await session.SendAsync("touch 0.5 0.3");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD on 340 layout=us-qwerty size=m route=text-input")));
        await session.SettleAsync();
    }

    [Fact]
    public async Task A_touch_on_the_field_raises_the_keyboard_and_shrinks_the_card_above_it()
    {
        using var config = new TempConfig(Config);
        using var session = await Field(config, "normal");
        await Raise(session);
        Assert.NotNull(await session.WaitForAsync(line => line == "INSET HEADLESS-1 340"));
        Assert.NotNull(await session.WaitForAsync(line => line == "CONFIGURE 1024 400"));
        var card = await session.WaitForAsync(line => Starts(line, "CARD 1 "), poke: "cards", fresh: true);
        Assert.Contains("x=512 y=228 scale=1", card!, StringComparison.Ordinal);
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }

    [Fact]
    public async Task A_pointer_click_on_the_field_leaves_the_keyboard_down()
    {
        using var config = new TempConfig(Config);
        using var session = await Field(config, "normal");
        await session.SendAsync("move 500 200");
        await session.SendAsync("button 272 1");
        await session.SendAsync("button 272 0");
        await session.SettleAsync();
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD off 0 "), poke: "keyboard", fresh: true));
        await session.SendAsync("quit");
    }

    [Fact]
    public async Task Typing_a_sentence_commits_it_with_auto_cap_and_leaves_shift_off()
    {
        using var config = new TempConfig(Config);
        using var session = await Field(config, "normal auto_capitalization");
        await Raise(session);
        await session.SendAsync("keyboard-type Hello, world.");
        Assert.NotNull(await session.WaitForAsync(line => line == "TEXT Hello, world."));
        Assert.NotNull(await session.WaitForAsync(line => line == "COMMIT H"));
        Assert.NotNull(await session.WaitForAsync(line => line == "KEY H text-input"));
        var state = await session.WaitForAsync(line => Starts(line, "  keyboard on "), poke: "where", fresh: true);
        Assert.Contains("shift=off", state!, StringComparison.Ordinal);
        await session.SendAsync("quit");
    }

    [Fact]
    public async Task Backspace_and_return_arrive_as_keys_and_double_space_types_a_period()
    {
        using var config = new TempConfig(Config);
        using var session = await Field(config, "normal");
        await Raise(session);
        await session.SendAsync("keyboard-type ab");
        Assert.NotNull(await session.WaitForAsync(line => line == "TEXT ab"));
        await session.SendAsync("keyboard-tap backspace");
        Assert.NotNull(await session.WaitForAsync(line => line == "KEY 14 1"));
        Assert.NotNull(await session.WaitForAsync(line => line == "TEXT a"));
        await session.SendAsync("keyboard-tap space");
        await session.SendAsync("keyboard-tap space");
        Assert.NotNull(await session.WaitForAsync(line => line == "COMMIT . "));
        Assert.NotNull(await session.WaitForAsync(line => line == "TEXT a. "));
        await session.SendAsync("keyboard-tap return");
        Assert.NotNull(await session.WaitForAsync(line => line == "KEY 28 1"));
        await session.SendAsync("quit");
    }

    [Fact]
    public async Task An_email_field_shows_dotcom_and_its_list_commits_dot_org()
    {
        using var config = new TempConfig(Config);
        using var session = await Field(config, "email");
        await Raise(session);
        await session.SendAsync("keyboard-tap @");
        Assert.NotNull(await session.WaitForAsync(line => line == "COMMIT @"));
        await session.SendAsync("keyboard-hold .com 400");
        Assert.NotNull(await session.WaitForAsync(line => line == "LONGPRESS .com,.net,.org,.edu"));
        await session.SendAsync("keyboard-choose 2");
        Assert.NotNull(await session.WaitForAsync(line => line == "COMMIT .org"));
        await session.SendAsync("quit");
    }

    [Fact]
    public async Task A_window_without_text_input_gets_keys_through_a_manual_raise()
    {
        var ssdwin = WlClient("ssdwin");
        using var config = new TempConfig(Config);
        using var session = new LunaSession(LunaSession.Compositor(), config.Path, config.Environment, "-c", ssdwin);
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "CARD + 1 ssdwin")));
        await session.SettleAsync();
        await session.SendAsync("keyboard show");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD on 340 layout=us-qwerty size=m route=keys")));
        await session.SendAsync("keyboard-type ab");
        Assert.NotNull(await session.WaitForAsync(line => line == "KEY 30 1"));
        Assert.NotNull(await session.WaitForAsync(line => line == "KEY 48 1"));
        await session.SendAsync("quit");
    }

    [Fact]
    public async Task A_hardware_key_hides_the_keyboard()
    {
        using var config = new TempConfig(Config);
        using var session = await Field(config, "normal");
        await Raise(session);
        await session.SendAsync("key 30 1");
        await session.SendAsync("key 30 0");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD off 0 ")));
        Assert.NotNull(await session.WaitForAsync(line => line == "INSET HEADLESS-1 0"));
        Assert.NotNull(await session.WaitForAsync(line => line == "CONFIGURE 1024 740"));
        await session.SendAsync("quit");
    }

    [Fact]
    public async Task The_bottom_right_swipe_raises_it_and_a_long_bottom_swipe_still_opens_the_card_view()
    {
        using var config = new TempConfig(Config);
        using var session = await Field(config, "normal");
        await session.SendAsync("touchdrag 0.95 0.99 0 -0.3 200");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD on 340 ")));
        await session.SettleAsync();
        await session.SendAsync("touchdrag 0.5 0.99 0 -0.05 150");
        Assert.NotNull(await session.WaitForAsync(line => line == "KEY space text-input"));
        await session.SendAsync("touchdrag 0.5 0.99 0 -0.3 200");
        Assert.NotNull(await session.WaitForAsync(line => line == "CARDVIEW on"));
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD off 0 ")));
        await session.SendAsync("quit");
    }

    [Fact]
    public async Task The_keybinding_and_the_app_menu_item_toggle_it()
    {
        using var config = new TempConfig(Config);
        using var session = await Field(config, "normal");
        await session.SendAsync("gesture keyboard");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD on 340 ")));
        var menu = await session.WaitForAsync(line => line == "  Hide Keyboard", poke: "appmenu", fresh: true);
        Assert.NotNull(menu);
        await session.SendAsync("appmenu-choose Hide Keyboard");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD off 0 ")));
        await session.SendAsync("quit");
    }

    [Fact]
    public async Task A_chosen_size_is_kept_in_the_state_file()
    {
        using var config = new TempConfig(Config);
        using (var session = await Field(config, "normal"))
        {
            await Raise(session);
            await session.SendAsync("keyboard-size l");
            Assert.NotNull(await session.WaitForAsync(line => line == "INSET HEADLESS-1 393"));
            await session.SendAsync("quit");
            Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
        }

        Assert.Contains("\"size\": \"l\"", File.ReadAllText(Path.Combine(config.Environment["XDG_STATE_HOME"], "luna-wm", "keyboard.json")), StringComparison.Ordinal);
        using var again = await Field(config, "normal");
        await again.SendAsync("touch 0.5 0.3");
        Assert.NotNull(await again.WaitForAsync(line => Starts(line, "KEYBOARD on 393 layout=us-qwerty size=l")));
        await again.SendAsync("quit");
    }

    [Fact]
    public async Task The_language_key_cycles_the_layouts_and_the_choice_is_kept()
    {
        using var config = new TempConfig(TempConfig.Quiet + """

            [keyboard]
            layouts = ["us-qwerty", "fr-azerty", "xkb"]
            sounds = false
            """);
        using (var session = await Field(config, "normal"))
        {
            await session.SendAsync("touch 0.5 0.3");
            Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD on 340 layout=us-qwerty")));
            await session.SendAsync("keyboard-tap lang");
            Assert.NotNull(await session.WaitForAsync(line => line == "LAYOUT fr-azerty"));
            await session.SendAsync("keyboard-type a");
            Assert.NotNull(await session.WaitForAsync(line => line == "COMMIT a"));
            await session.SendAsync("keyboard-hold lang 800");
            Assert.NotNull(await session.WaitForAsync(line => line == "LONGPRESS En,Fr,Xkb"));
            await session.SendAsync("keyboard-choose 2");
            Assert.NotNull(await session.WaitForAsync(line => line == "LAYOUT xkb"));
            await session.SendAsync("keyboard-layout fr-azerty");
            Assert.NotNull(await session.WaitForAsync(line => line == "LAYOUT fr-azerty"));
            await session.SendAsync("quit");
            Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
        }

        using var again = await Field(config, "normal");
        await again.SendAsync("touch 0.5 0.3");
        Assert.NotNull(await again.WaitForAsync(line => Starts(line, "KEYBOARD on 340 layout=fr-azerty")));
        await again.SendAsync("quit");
    }

    [Fact]
    public async Task The_hide_key_list_sets_the_size_at_once()
    {
        using var config = new TempConfig(Config);
        using var session = await Field(config, "normal");
        await Raise(session);
        await session.SendAsync("keyboard-hold hide 800");
        Assert.NotNull(await session.WaitForAsync(line => line == "LONGPRESS XS,S,M,L"));
        await session.SendAsync("keyboard-choose 0");
        Assert.NotNull(await session.WaitForAsync(line => line == "KBSIZE xs"));
        Assert.NotNull(await session.WaitForAsync(line => line == "INSET HEADLESS-1 243"));
        Assert.NotNull(await session.WaitForAsync(line => line == "CONFIGURE 1024 497"));
        await session.SendAsync("keyboard-tap hide");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD off 0 ")));
        await session.SendAsync("quit");
    }

    [Fact]
    public async Task With_fcitx5_on_the_bus_keys_go_through_it_and_a_candidate_commits_a_word()
    {
        var fcitx5 = LunaSession.Client("fcitx5");
        var textinput = WlClient("textinput");
        using var bus = PrivateBus.Start();
        using var config = new TempConfig(Config);
        var environment = Fcitx5Environment(config, bus);
        using var session = new LunaSession(LunaSession.Compositor(), config.Path, environment, "-c", $"{fcitx5} --replace >/dev/null 2>&1 & sleep 2; exec {textinput} normal");
        Assert.NotNull(await session.WaitForAsync(line => line == "ENTER", attempts: 200));
        await session.SendAsync("touch 0.5 0.3");
        Assert.NotNull(await session.WaitForAsync(line => line == "FCITX5 backend=on", attempts: 100));
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD on 395 ") && line.Contains("route=fcitx5", StringComparison.Ordinal), attempts: 100));
        Assert.NotNull(await session.WaitForAsync(line => line == "FCITX5 im=keyboard-us", attempts: 50));
        await session.SendAsync("keyboard-type hel");
        var candidates = await session.WaitForAsync(line => Starts(line, "CANDIDATES ") && line.EndsWith(" first=hel", StringComparison.Ordinal));
        Assert.NotNull(candidates);
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD on 395 ") && line.Contains("predict=fcitx5", StringComparison.Ordinal), poke: "keyboard", fresh: true));
        await session.SendAsync("keyboard-candidate 0");
        Assert.NotNull(await session.WaitForAsync(line => line == "COMMIT hel"));
        Assert.NotNull(await session.WaitForAsync(line => line == "KEY space fcitx5"));
        Assert.NotNull(await session.WaitForAsync(line => line == "KEY 57 1"));
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "KEYBOARD on 395 "), poke: "keyboard", fresh: true));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }

    [Fact]
    public async Task A_traced_word_is_committed_with_a_space()
    {
        Assert.SkipUnless(File.Exists("/usr/share/fcitx5/spell/en_dict.fscd") || File.Exists("/usr/share/hunspell/en_US.dic"), "no English word list is installed");
        using var config = new TempConfig(Config);
        using var session = await Field(config, "normal");
        await Raise(session);
        Assert.NotNull(await session.WaitForAsync(line => line.Contains("trace typing for 'en'", StringComparison.Ordinal) || line.StartsWith("ENTER", StringComparison.Ordinal), attempts: 50));
        string? traced = null;
        for (var attempt = 0; attempt < 30 && traced is null; attempt++)
        {
            await session.SendAsync("keyboard-trace hello");
            traced = await session.WaitForAsync(line => Starts(line, "TRACE hello"), attempts: 3);
        }

        Assert.NotNull(traced);
        Assert.NotNull(await session.WaitForAsync(line => line == "COMMIT hello "));
        await session.SendAsync("touchdrag 0.546 0.755 -0.30 -0.09 300");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "TRACE he")));
        await session.SendAsync("quit");
    }

    internal static Dictionary<string, string> Fcitx5Environment(TempConfig config, PrivateBus bus, string first = "keyboard-us")
    {
        var fcitxHome = Path.Combine(config.Directory, "fcitx-config");
        Directory.CreateDirectory(Path.Combine(fcitxHome, "fcitx5", "conf"));
        File.WriteAllText(Path.Combine(fcitxHome, "fcitx5", "profile"), """
            [Groups/0]
            Name=Default
            Default Layout=us
            DefaultIM={first}

            [Groups/0/Items/0]
            Name={first}
            Layout=

            [Groups/0/Items/1]
            Name=keyboard-us
            Layout=

            [GroupOrder]
            0=Default
            """.Replace("{first}", first, StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(fcitxHome, "fcitx5", "conf", "keyboard.conf"), "EnableHintByDefault=True\n");
        var environment = config.Environment;
        environment["DBUS_SESSION_BUS_ADDRESS"] = bus.Address;
        environment["XDG_CONFIG_HOME"] = fcitxHome;
        environment["DISPLAY"] = "";
        return environment;
    }

    [Fact]
    public async Task With_pinyin_the_bar_offers_hanzi_and_a_choice_commits_them()
    {
        var fcitx5 = LunaSession.Client("fcitx5");
        Assert.SkipUnless(File.Exists("/usr/share/fcitx5/inputmethod/pinyin.conf"), "fcitx5-chinese-addons is not installed");
        var textinput = WlClient("textinput");
        using var bus = PrivateBus.Start();
        using var config = new TempConfig(Config);
        var environment = Fcitx5Environment(config, bus, "pinyin");
        using var session = new LunaSession(LunaSession.Compositor(), config.Path, environment, "-c", $"{fcitx5} --replace >/dev/null 2>&1 & sleep 2; exec {textinput} normal");
        Assert.NotNull(await session.WaitForAsync(line => line == "ENTER", attempts: 200));
        await session.SendAsync("touch 0.5 0.3");
        Assert.NotNull(await session.WaitForAsync(line => line == "FCITX5 im=pinyin", attempts: 100));
        await session.SendAsync("keyboard-type nihao");
        Assert.NotNull(await session.WaitForAsync(line => line == "PREEDIT ni hao 0"));
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "CANDIDATES ") && line.EndsWith(" first=你好", StringComparison.Ordinal)));
        await session.SendAsync("keyboard-candidate 0");
        Assert.NotNull(await session.WaitForAsync(line => line == "COMMIT 你好"));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }

    [Fact]
    public async Task Without_fcitx5_the_keyboard_is_plain_and_says_why()
    {
        using var bus = PrivateBus.Start();
        using var config = new TempConfig(Config);
        var environment = config.Environment;
        environment["DBUS_SESSION_BUS_ADDRESS"] = bus.Address;
        var textinput = WlClient("textinput");
        using var session = new LunaSession(LunaSession.Compositor(), config.Path, environment, "-c", $"{textinput} normal");
        Assert.NotNull(await session.WaitForAsync(line => line == "ENTER"));
        await session.SettleAsync();
        await session.SendAsync("touch 0.5 0.3");
        var line = await session.WaitForAsync(line => Starts(line, "KEYBOARD on 340 "));
        Assert.Contains("route=text-input predict=none reason=fcitx5 has not taken the keyboard", line!, StringComparison.Ordinal);
        await session.SendAsync("quit");
    }
}
