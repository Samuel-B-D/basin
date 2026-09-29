using Basin.Capabilities;
using Basin.Config;
using Basin.Diagnostics;
using Tomlyn;
using Xunit;

namespace Basin.Tests;

public sealed class InputConfigTests
{
    private const string Example = """
        [input.touchpad]
        tap = true
        natural_scroll = true
        click_method = "clickfinger"
        dwt = true

        [input.pointer]
        accel_profile = "adaptive"

        [input.keyboard]
        layout = "us,de"
        options = "grp:alt_shift_toggle"
        repeat_rate = 25
        repeat_delay = 600

        [[input.device]]
        name = "Logitech*"
        accel_profile = "flat"
        accel_speed = -0.3

        [[input.device]]
        vendor = 0x056a
        product = 0x0357
        map_to_output = "DP-2"
        """;

    private sealed class CollectingSink : IBasinLogSink
    {
        public List<string> Lines { get; } = [];

        public void Write(BasinLogLevel level, string category, ReadOnlySpan<char> message) =>
            Lines.Add($"{level}:{message}");
    }

    private static (BasinLogger Log, CollectingSink Sink) Logger()
    {
        var sink = new CollectingSink();
        BasinLog.Sink = sink;
        BasinLog.Level = BasinLogLevel.Trace;
        return (BasinLog.For("test"), sink);
    }

    private static IReadOnlyList<InputRule> Read(string text, BasinLogger log, bool reportUnknown = true)
    {
        var reader = new TomlReader(Toml.ToModel(text), log);
        var rules = reader.Section("input") is { } input ? InputConfig.Read(input, log) : [];
        if (reportUnknown)
        {
            reader.ReportUnknown();
        }

        return rules;
    }

    [Fact]
    public void The_spec_example_parses_to_the_expected_rules()
    {
        var (log, sink) = Logger();
        try
        {
            var rules = Read(Example, log);

            Assert.Empty(sink.Lines);
            Assert.Equal(5, rules.Count);

            Assert.Equal(new InputMatch(Type: InputDeviceType.Touchpad), rules[0].Match);
            Assert.Equal(
                [InputSetting.Tap, InputSetting.NaturalScroll, InputSetting.ClickMethod, InputSetting.DisableWhileTyping],
                rules[0].Settings.Select(s => s.Setting));
            Assert.Equal((uint)InputClickMethod.Clickfinger, rules[0].Settings[2].Value.Value);

            Assert.Equal(new InputMatch(Type: InputDeviceType.Pointer), rules[1].Match);
            Assert.Equal((uint)InputAccelProfile.Adaptive, rules[1].Settings[0].Value.Value);

            Assert.True(rules[2].Match.AnyKeyboard);
            Assert.Equal("us,de", rules[2].Settings[0].Value.TextValue);
            Assert.Equal(600u, rules[2].Settings[3].Value.Value);

            Assert.Equal("Logitech*", rules[3].Match.NameGlob);
            Assert.Equal(-0.3, rules[3].Settings[1].Value[0]);

            Assert.Equal((0x056au, 0x0357u), (rules[4].Match.Vendor!.Value, rules[4].Match.Product!.Value));
            Assert.Equal("DP-2", rules[4].Settings[0].Value.TextValue);
        }
        finally
        {
            BasinLog.Sink = null;
        }
    }

    [Fact]
    public void Top_level_keys_form_the_catch_all_rule()
    {
        var (log, _) = Logger();
        try
        {
            var rules = Read("[input]\nnatural_scroll = true\n\n[input.touchpad]\ntap = true\n", log);

            Assert.Equal(2, rules.Count);
            Assert.True(rules[0].Match.IsCatchAll);
            Assert.Equal(InputSetting.NaturalScroll, rules[0].Settings[0].Setting);
        }
        finally
        {
            BasinLog.Sink = null;
        }
    }

    [Fact]
    public void An_unknown_key_warns_and_an_unknown_word_drops_that_key_alone()
    {
        var (log, sink) = Logger();
        try
        {
            var rules = Read("[input.touchpad]\ntap = true\ntap_to_click = true\nclick_method = \"fingers\"\n", log);

            var rule = Assert.Single(rules);
            Assert.Equal(InputSetting.Tap, Assert.Single(rule.Settings).Setting);
            Assert.Contains(sink.Lines, line => line.Contains("unknown key 'input.touchpad.tap_to_click'", StringComparison.Ordinal));
            Assert.Contains(sink.Lines, line => line.Contains("input.touchpad.click_method", StringComparison.Ordinal));
        }
        finally
        {
            BasinLog.Sink = null;
        }
    }

    [Fact]
    public void A_device_entry_without_a_match_key_is_ignored()
    {
        var (log, sink) = Logger();
        try
        {
            var rules = Read("[[input.device]]\ntap = true\n", log);

            Assert.Empty(rules);
            Assert.Contains(sink.Lines, line => line.Contains("needs name", StringComparison.Ordinal));
        }
        finally
        {
            BasinLog.Sink = null;
        }
    }

    [Fact]
    public void Set_splices_one_key_into_a_type_table_and_a_named_device_entry()
    {
        var doc = TomlDocument.Parse("# my input\n[input.touchpad]\ntap = true # keep this\n");

        InputConfig.Set(doc, new InputMatch(Type: InputDeviceType.Touchpad), InputSetting.NaturalScroll, InputSettingValue.Flag(true));
        InputConfig.Set(doc, new InputMatch(NameGlob: "Logitech G502"), InputSetting.AccelSpeed, InputSettingValue.Number(-0.25));
        InputConfig.Set(doc, new InputMatch(NameGlob: "Logitech G502"), InputSetting.AccelProfile, InputSettingValue.Of(InputAccelProfile.Flat));
        InputConfig.Set(doc, new InputMatch(AnyKeyboard: true), InputSetting.RepeatRate, InputSettingValue.Of(40u));

        var text = doc.ToString();
        Assert.Contains("tap = true # keep this", text, StringComparison.Ordinal);
        Assert.Contains("# my input", text, StringComparison.Ordinal);
        Assert.Equal(1, doc.TableCount(InputConfig.DeviceTable));

        var (log, sink) = Logger();
        try
        {
            var rules = Read(text, log);
            Assert.Empty(sink.Lines);
            var touchpad = rules.Single(r => r.Match.Type == InputDeviceType.Touchpad);
            Assert.True(touchpad.Names(InputSetting.NaturalScroll, out var natural) && natural.IsOn);
            var device = rules.Single(r => r.Match.NameGlob == "Logitech G502");
            Assert.True(device.Names(InputSetting.AccelSpeed, out var speed));
            Assert.Equal(-0.25, speed[0]);
            Assert.True(device.Names(InputSetting.AccelProfile, out var profile));
            Assert.Equal((uint)InputAccelProfile.Flat, profile.Value);
            Assert.True(rules.Single(r => r.Match.AnyKeyboard).Names(InputSetting.RepeatRate, out var rate));
            Assert.Equal(40u, rate.Value);
        }
        finally
        {
            BasinLog.Sink = null;
        }

        Assert.True(InputConfig.Remove(doc, new InputMatch(NameGlob: "Logitech G502"), InputSetting.AccelSpeed));
        Assert.DoesNotContain("accel_speed", doc.ToString(), StringComparison.Ordinal);
    }
}
