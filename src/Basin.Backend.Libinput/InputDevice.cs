using Basin.Session;
using Libinput;
using Udev;

namespace Basin.Backend.Libinput;

public sealed class InputDevice
{
    internal InputDevice(LibinputDevice native)
    {
        Native = native;
        Name = native.Name;
        HasKeyboard = native.HasCapability(LibinputDeviceCapability.Keyboard);
        HasPointer = native.HasCapability(LibinputDeviceCapability.Pointer);
        HasTouch = native.HasCapability(LibinputDeviceCapability.Touch);
        HasGesture = native.HasCapability(LibinputDeviceCapability.Gesture);
        HasSwitch = native.HasCapability(LibinputDeviceCapability.Switch);
        HasTabletTool = native.HasCapability(LibinputDeviceCapability.TabletTool);
        HasTabletPad = native.HasCapability(LibinputDeviceCapability.TabletPad);
        Sysname = native.Sysname;
        Vendor = native.IdVendor;
        Product = native.IdProduct;
        Bus = native.IdBustype;
    }

    internal LibinputDevice Native { get; }

    public string Name { get; }

    public string Sysname { get; }

    public uint Vendor { get; }

    public uint Product { get; }

    public uint Bus { get; }

    public bool HasKeyboard { get; }

    public bool HasPointer { get; }

    public bool HasTouch { get; }

    public bool HasGesture { get; }

    public bool HasSwitch { get; }

    public bool HasTabletTool { get; }

    public bool HasTabletPad { get; }

    public LibinputDeviceConfig Config => Native.Config;

    public void UpdateLeds(LibinputLed leds) => Native.UpdateLeds(leds);

    public string? OutputName => Native.OutputName;
}
