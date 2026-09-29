namespace Basin.Capabilities;

public static class InputDeviceTypes
{
    public static InputDeviceType Of(InputDeviceCapability capabilities, int tapFingerCount)
    {
        if ((capabilities & InputDeviceCapability.TabletTool) != 0)
        {
            return InputDeviceType.TabletTool;
        }

        if ((capabilities & InputDeviceCapability.TabletPad) != 0)
        {
            return InputDeviceType.TabletPad;
        }

        if ((capabilities & InputDeviceCapability.Touch) != 0)
        {
            return InputDeviceType.Touch;
        }

        if ((capabilities & InputDeviceCapability.Pointer) != 0)
        {
            return tapFingerCount > 0 ? InputDeviceType.Touchpad : InputDeviceType.Pointer;
        }

        if ((capabilities & InputDeviceCapability.Keyboard) != 0)
        {
            return InputDeviceType.Keyboard;
        }

        return InputDeviceType.Switch;
    }
}
