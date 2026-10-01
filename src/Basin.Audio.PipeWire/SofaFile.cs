using System.Runtime.InteropServices;

namespace Basin.Audio.PipeWire;

public static unsafe class SofaFile
{
    private const string Library = "libmysofa.so.1";

    public static bool TryProbe(string path, out float radiusMin, out float radiusMax, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(path);
        radiusMin = radiusMax = 1;
        if (!File.Exists(path))
        {
            reason = $"{path} does not exist";
            return false;
        }

        if (!NativeLibrary.TryLoad(Library, out var library))
        {
            reason = $"{Library} is not installed";
            return false;
        }

        try
        {
            if (!NativeLibrary.TryGetExport(library, "mysofa_open", out var open) ||
                !NativeLibrary.TryGetExport(library, "mysofa_close", out var close))
            {
                reason = $"{Library} has no mysofa_open";
                return false;
            }

            var openFunction = (delegate* unmanaged<byte*, float, int*, int*, Easy*>)open;
            var closeFunction = (delegate* unmanaged<Easy*, void>)close;
            var name = System.Text.Encoding.UTF8.GetBytes(path + "\0");
            int length, error;
            Easy* easy;
            fixed (byte* file = name)
            {
                easy = openFunction(file, 48000, &length, &error);
            }

            if (easy is null || error != 0)
            {
                reason = $"libmysofa cannot read {path} (error {error})";
                if (easy is not null)
                {
                    closeFunction(easy);
                }

                return false;
            }

            if (easy->Lookup is not null)
            {
                radiusMin = easy->Lookup->RadiusMin;
                radiusMax = easy->Lookup->RadiusMax;
            }

            closeFunction(easy);
            reason = null;
            return true;
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Easy
    {
        public void* Hrtf;
        public Lookup* Lookup;
        public void* Neighborhood;
        public float* Fir;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Lookup
    {
        public void* Kdtree;
        public float RadiusMin;
        public float RadiusMax;
        public float ThetaMin;
        public float ThetaMax;
        public float PhiMin;
        public float PhiMax;
    }
}
