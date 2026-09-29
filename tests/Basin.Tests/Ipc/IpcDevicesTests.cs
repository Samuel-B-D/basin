using System.Text.Json;
using Basin.Capabilities;
using Basin.Ipc;
using Xunit;

namespace Basin.Tests;

public sealed class IpcDevicesTests
{
    private static JsonElement Parse(string frame) => JsonDocument.Parse(frame).RootElement.Clone();

    private static JsonElement Result(IpcTestPeer peer, string request) => Parse(peer.Call(request)).GetProperty("result");

    private static (IpcTestRig Rig, FakeInputDeviceConfiguration Devices, ulong Touchpad, ulong Mouse) Create()
    {
        var devices = new FakeInputDeviceConfiguration();
        var touchpad = devices.AddTouchpad();
        var mouse = devices.AddMouse();
        var rig = new IpcTestRig(services => services.Use<IInputDeviceConfiguration>(devices));
        return (rig, devices, touchpad, mouse);
    }

    [Fact]
    public void List_names_every_device_with_its_type_and_identity()
    {
        var (rig, _, touchpad, mouse) = Create();
        using var owned = rig;
        var peer = rig.Connect();

        var devices = Result(peer, """{"method":"devices/list"}""").GetProperty("devices");

        Assert.Equal(2, devices.GetArrayLength());
        var first = devices.EnumerateArray().Single(d => d.GetProperty("id").GetUInt64() == touchpad);
        Assert.Equal("touchpad", first.GetProperty("type").GetString());
        Assert.Equal(["pointer", "gesture"], first.GetProperty("capabilities").EnumerateArray().Select(e => e.GetString()));
        var second = devices.EnumerateArray().Single(d => d.GetProperty("id").GetUInt64() == mouse);
        Assert.Equal(0x046d, second.GetProperty("vendor").GetInt32());
    }

    [Fact]
    public void Get_answers_current_default_source_and_words_for_supported_settings_only()
    {
        var (rig, devices, touchpad, _) = Create();
        using var owned = rig;
        devices.Set(touchpad, InputSetting.Tap, InputSettingValue.Flag(true));
        var peer = rig.Connect();

        var result = Result(peer, $$$"""{"method":"devices/get","params":{"device":{{{touchpad}}}}}""");
        var settings = result.GetProperty("settings").EnumerateArray().ToDictionary(s => s.GetProperty("key").GetString()!);

        Assert.Equal("true", settings["tap"].GetProperty("current").GetString());
        Assert.Equal("false", settings["tap"].GetProperty("default").GetString());
        Assert.Equal("runtime", settings["tap"].GetProperty("source").GetString());
        Assert.Equal("default", settings["dwt"].GetProperty("source").GetString());
        Assert.Contains("clickfinger", settings["click_method"].GetProperty("values").EnumerateArray().Select(e => e.GetString()));
        Assert.False(settings.ContainsKey("rotation"));

        var missing = Parse(peer.Call("""{"method":"devices/get","params":{"device":99}}"""));
        Assert.Equal(IpcErrorCodes.NotFound, missing.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void Set_answers_per_key_and_a_type_target_reaches_every_device_of_that_type()
    {
        var (rig, devices, touchpad, mouse) = Create();
        using var owned = rig;
        var peer = rig.Connect();

        var one = Result(peer, $$$$"""{"method":"devices/set","params":{"device":{{{{touchpad}}}},"settings":{"tap":true,"accel_speed":-0.25,"rotation":90,"click_method":"fingers","nonsense":1}}}""");
        var outcomes = one.GetProperty("results").EnumerateArray().ToDictionary(r => r.GetProperty("key").GetString()!, r => r.GetProperty("result").GetString());
        Assert.Equal("ok", outcomes["tap"]);
        Assert.Equal("ok", outcomes["accel_speed"]);
        Assert.Equal("unsupported", outcomes["rotation"]);
        Assert.Equal("invalid", outcomes["click_method"]);
        Assert.Equal("invalid", outcomes["nonsense"]);
        Assert.True(devices.Current(touchpad, InputSetting.Tap).IsOn);
        Assert.Equal(-0.25, devices.Current(touchpad, InputSetting.AccelSpeed)[0]);

        var all = Result(peer, """{"method":"devices/set","params":{"type":"pointer","settings":{"natural-scroll":"enabled"}}}""");
        var target = Assert.Single(all.GetProperty("results").EnumerateArray());
        Assert.Equal(mouse, target.GetProperty("device").GetUInt64());
        Assert.True(devices.Current(mouse, InputSetting.NaturalScroll).IsOn);
        Assert.False(devices.Current(touchpad, InputSetting.NaturalScroll).IsOn);

        var neither = Parse(peer.Call("""{"method":"devices/set","params":{"settings":{"tap":true}}}"""));
        Assert.Equal(IpcErrorCodes.InvalidParams, neither.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void Reset_restores_named_keys()
    {
        var (rig, devices, touchpad, _) = Create();
        using var owned = rig;
        var peer = rig.Connect();
        _ = Result(peer, $$$$"""{"method":"devices/set","params":{"device":{{{{touchpad}}}},"settings":{"tap":true}}}""");

        var reset = Result(peer, $$$"""{"method":"devices/reset","params":{"device":{{{touchpad}}},"keys":["tap"]}}""");

        Assert.Equal("ok", reset.GetProperty("results")[0].GetProperty("result").GetString());
        Assert.False(devices.Current(touchpad, InputSetting.Tap).IsOn);
    }

    [Fact]
    public void Added_removed_and_changed_reach_a_subscriber()
    {
        var (rig, devices, touchpad, _) = Create();
        using var owned = rig;
        var peer = rig.Connect();
        _ = Result(peer, """{"method":"ipc/subscribe","params":{"events":["devices/added","devices/removed","devices/changed"]}}""");

        devices.Set(touchpad, InputSetting.Tap, InputSettingValue.Flag(true));
        devices.Set(touchpad, InputSetting.Tap, InputSettingValue.Flag(false));
        devices.Set(touchpad, InputSetting.Tap, InputSettingValue.Flag(true));
        var keyboard = devices.AddKeyboard();
        devices.Remove(keyboard);

        var changed = Parse(peer.Receive());
        Assert.Equal(IpcEventNames.DeviceChanged, changed.GetProperty("event").GetString());
        Assert.Equal("tap", changed.GetProperty("data").GetProperty("key").GetString());
        Assert.Equal("true", changed.GetProperty("data").GetProperty("value").GetString());

        var added = Parse(peer.Receive());
        Assert.Equal(IpcEventNames.DeviceAdded, added.GetProperty("event").GetString());
        Assert.Equal("keyboard", added.GetProperty("data").GetProperty("type").GetString());

        var removed = Parse(peer.Receive());
        Assert.Equal(IpcEventNames.DeviceRemoved, removed.GetProperty("event").GetString());
        Assert.Equal(keyboard, removed.GetProperty("data").GetProperty("id").GetUInt64());
        Assert.False(peer.HasFrame());
    }
}
