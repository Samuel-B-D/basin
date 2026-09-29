using Basin.Capabilities;
using Xunit;

namespace Basin.Tests;

public sealed class InputSettingNamesTests
{
    [Fact]
    public void Every_setting_key_round_trips()
    {
        foreach (var setting in InputSettingNames.Settings)
        {
            var key = InputSettingNames.Key(setting);
            Assert.True(InputSettingNames.TryParseKey(key, out var parsed), key);
            Assert.Equal(setting, parsed);
            Assert.True(InputSettingNames.TryParseKey(key.Replace('_', '-'), out parsed), key);
            Assert.Equal(setting, parsed);
        }
    }

    [Fact]
    public void Every_value_word_round_trips()
    {
        foreach (var setting in InputSettingNames.Settings)
        {
            foreach (var word in InputSettingNames.Words(setting))
            {
                Assert.True(InputSettingNames.TryParse(setting, word, out var value), $"{setting} {word}");
                Assert.Equal(word, InputSettingNames.Format(setting, value));
                Assert.True(InputSettingNames.TryParse(setting, word.Replace('_', '-'), out var dashed));
                Assert.Equal(value, dashed);
            }
        }
    }

    [Fact]
    public void Every_device_type_word_round_trips()
    {
        foreach (var type in InputSettingNames.Types)
        {
            var name = InputSettingNames.TypeName(type);
            Assert.True(InputSettingNames.TryParseType(name, out var parsed));
            Assert.Equal(type, parsed);
        }
    }

    [Theory]
    [InlineData(InputSetting.AccelSpeed, "-0.3", "-0.3")]
    [InlineData(InputSetting.ScrollFactor, "1.5", "1.5")]
    [InlineData(InputSetting.RepeatRate, "25", "25")]
    [InlineData(InputSetting.Rotation, "90", "90")]
    [InlineData(InputSetting.CalibrationMatrix, "1 0 0, 0 1 0", "1 0 0 0 1 0")]
    [InlineData(InputSetting.KeymapLayout, "us,de", "us,de")]
    [InlineData(InputSetting.Tap, "enabled", "true")]
    [InlineData(InputSetting.NaturalScroll, "disabled", "false")]
    [InlineData(InputSetting.ScrollMethod, "two-finger", "two_finger")]
    [InlineData(InputSetting.SendEvents, "disabled-on-external-mouse", "disabled_on_external_mouse")]
    public void Values_parse_and_format_canonically(InputSetting setting, string text, string canonical)
    {
        Assert.True(InputSettingNames.TryParse(setting, text, out var value));
        Assert.Equal(canonical, InputSettingNames.Format(setting, value));
    }

    [Theory]
    [InlineData(InputSetting.Tap, "maybe")]
    [InlineData(InputSetting.ClickMethod, "fingers")]
    [InlineData(InputSetting.AccelSpeed, "2")]
    [InlineData(InputSetting.AccelSpeed, "fast")]
    [InlineData(InputSetting.ScrollFactor, "0")]
    [InlineData(InputSetting.Rotation, "360")]
    [InlineData(InputSetting.RepeatDelay, "-1")]
    [InlineData(InputSetting.CalibrationMatrix, "1 0 0 0 1")]
    public void An_unknown_word_fails(InputSetting setting, string text)
    {
        Assert.False(InputSettingNames.TryParse(setting, text, out _));
    }

    [Fact]
    public void An_unknown_key_fails()
    {
        Assert.False(InputSettingNames.TryParseKey("tap_to_click", out _));
    }

    [Fact]
    public void Enum_values_keep_libinput_numbering()
    {
        Assert.Equal(4u, (uint)InputScrollMethod.OnButtonDown);
        Assert.Equal(2u, (uint)InputScrollMethod.Edge);
        Assert.Equal(2u, (uint)InputClickMethod.Clickfinger);
        Assert.Equal(4u, (uint)InputAccelProfile.Custom);
        Assert.Equal(2u, (uint)InputSendEventsMode.DisabledOnExternalMouse);
    }

    [Fact]
    public void A_touchpad_is_a_pointer_with_tap_fingers()
    {
        Assert.Equal(InputDeviceType.Touchpad, InputDeviceTypes.Of(InputDeviceCapability.Pointer | InputDeviceCapability.Gesture, 3));
        Assert.Equal(InputDeviceType.Pointer, InputDeviceTypes.Of(InputDeviceCapability.Pointer, 0));
        Assert.Equal(InputDeviceType.Pointer, InputDeviceTypes.Of(InputDeviceCapability.Pointer | InputDeviceCapability.Keyboard, 0));
        Assert.Equal(InputDeviceType.TabletTool, InputDeviceTypes.Of(InputDeviceCapability.TabletTool | InputDeviceCapability.Pointer, 0));
        Assert.Equal(InputDeviceType.Keyboard, InputDeviceTypes.Of(InputDeviceCapability.Keyboard, 0));
    }

    [Fact]
    public void A_value_holds_six_numbers_without_allocating()
    {
        ReadOnlySpan<double> six = [1, 2, 3, 4, 5, 6];
        InputSettingValue.Matrix(six);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var matrix = InputSettingValue.Matrix(six);
        var number = InputSettingValue.Number(0.5);
        var equal = matrix.Equals(InputSettingValue.Matrix(six));
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.True(equal);
        Assert.Equal(6, matrix.Count);
        Assert.Equal(6.0, matrix[5]);
        Assert.Equal(0.5, number[0]);
    }
}
