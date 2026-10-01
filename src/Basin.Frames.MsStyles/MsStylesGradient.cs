using Basin.Themes.MsStyles;

namespace Basin.Frames.MsStyles;

public struct MsStylesGradient : IEquatable<MsStylesGradient>
{
    public FillType Kind;
    public int Count;
    public uint Color0;
    public uint Color1;
    public uint Color2;
    public uint Color3;
    public uint Color4;
    public int Ratio0;
    public int Ratio1;
    public int Ratio2;
    public int Ratio3;
    public int Ratio4;

    public readonly uint ColorAt(int index) => index switch
    {
        0 => Color0,
        1 => Color1,
        2 => Color2,
        3 => Color3,
        _ => Color4,
    };

    public readonly int RatioAt(int index) => index switch
    {
        0 => Ratio0,
        1 => Ratio1,
        2 => Ratio2,
        3 => Ratio3,
        _ => Ratio4,
    };

    public void Set(int index, uint color, int ratio)
    {
        switch (index)
        {
            case 0: Color0 = color; Ratio0 = ratio; break;
            case 1: Color1 = color; Ratio1 = ratio; break;
            case 2: Color2 = color; Ratio2 = ratio; break;
            case 3: Color3 = color; Ratio3 = ratio; break;
            default: Color4 = color; Ratio4 = ratio; break;
        }
    }

    public readonly bool Equals(MsStylesGradient other) =>
        Kind == other.Kind && Count == other.Count &&
        Color0 == other.Color0 && Color1 == other.Color1 && Color2 == other.Color2 && Color3 == other.Color3 && Color4 == other.Color4 &&
        Ratio0 == other.Ratio0 && Ratio1 == other.Ratio1 && Ratio2 == other.Ratio2 && Ratio3 == other.Ratio3 && Ratio4 == other.Ratio4;

    public override readonly bool Equals(object? obj) => obj is MsStylesGradient other && Equals(other);

    public override readonly int GetHashCode() =>
        HashCode.Combine(Kind, Count, Color0, Color1, Color2, Color3, Color4, HashCode.Combine(Ratio0, Ratio1, Ratio2, Ratio3, Ratio4));

    public static bool operator ==(MsStylesGradient left, MsStylesGradient right) => left.Equals(right);

    public static bool operator !=(MsStylesGradient left, MsStylesGradient right) => !left.Equals(right);
}
