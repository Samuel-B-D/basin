using System.Numerics;
using System.Runtime.InteropServices;

namespace Basin.Scene3D;

[StructLayout(LayoutKind.Sequential)]
public struct SkinnedVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 Uv;
    public Vector4 Joints;
    public Vector4 Weights;
}
