using System.Numerics;

namespace Basin.Scene3D;

internal static class WorldFrameShading
{
    public static readonly Vector4 BackColor = new(0.08f, 0.07f, 0.06f, 1);

    public static void Frame(WorldWindow window, WorldWindowFrame frame, out Vector4 albedo, out Vector3 emissive)
    {
        var material = frame.Material;
        albedo = material.Albedo;
        emissive = material.Emissive;
        if (window.Focused)
        {
            var focus = window.FocusColor;
            albedo = Vector4.Lerp(albedo, focus with { W = albedo.W }, 0.35f);
            emissive += new Vector3(focus.X, focus.Y, focus.Z) * 0.2f;
        }

        if (window.Flash > 0)
        {
            albedo = Vector4.Lerp(albedo, Vector4.One, Math.Clamp(window.Flash, 0, 1));
        }

        albedo.W *= window.Opacity;
    }

    public static Matrix4x4 Back(WorldWindow window, WorldWindowFrame frame)
    {
        var outer = frame.OuterSize(window.Width, window.Height);
        return Matrix4x4.CreateScale(outer.X, outer.Y, frame.Depth) * window.Model;
    }

    public static Matrix4x4 PlaqueBody(WorldWindow window, WorldWindowFrame frame)
    {
        var center = frame.PlaqueCenter(window.Height);
        return Matrix4x4.CreateScale(frame.PlaqueWidth, frame.PlaqueHeight, frame.PlaqueThickness) *
            Matrix4x4.CreateTranslation(center) * window.Model;
    }

    public static Matrix4x4 PlaqueFace(WorldWindow window, WorldWindowFrame frame)
    {
        var center = frame.PlaqueCenter(window.Height);
        return Matrix4x4.CreateScale(frame.PlaqueWidth, frame.PlaqueHeight, 1) *
            Matrix4x4.CreateTranslation(center.X, center.Y, center.Z + 0.0002f) * window.Model;
    }
}
