using System.Numerics;
using System.Runtime.InteropServices;

namespace Basin.Scene3D;

[StructLayout(LayoutKind.Sequential)]
public struct WorldVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 Uv;

    public WorldVertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        Position = position;
        Normal = normal;
        Uv = uv;
    }
}
