namespace Basin.Scene;

public static class DevicePlacement
{
    public static int ChromeScale(double scale) => Math.Max(1, (int)Math.Round(scale));

    public static int ToDevice(int logical, double scale) => (int)Math.Round(logical * scale);

    public static int ToDeviceSpan(int logical, int length, double scale) =>
        ToDevice(logical + length, scale) - ToDevice(logical, scale);

    public static (int Logical, int Nudge) FromDevice(int device, double scale)
    {
        var logical = (int)Math.Floor(device / scale);
        return (logical, device - ToDevice(logical, scale));
    }

    public static int ToLogicalSpan(int device, double scale) => (int)Math.Ceiling(device / scale);

    public static (int Width, int Height) OutputSize(IOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return OutputTransforms.SwapsAxes(output.Transform)
            ? (output.CurrentMode.Height, output.CurrentMode.Width)
            : (output.CurrentMode.Width, output.CurrentMode.Height);
    }
}
