using System.Numerics;

namespace Basin.Scene3D;

public readonly record struct WorldMaterial(Vector4 Albedo, float SpecularStrength, float Shininess, Vector3 Emissive)
{
    public static WorldMaterial Default => new(Vector4.One, 0.25f, 24, Vector3.Zero);
}
