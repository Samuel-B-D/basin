namespace Basin.Scene3D;

public struct WorldSurfaceLayer
{
    public Surface? Surface;
    public ITexture? Texture;
    public float X;
    public float Y;
    public float Width;
    public float Height;
    public FBox Source;
    public int Level;
    public bool Opaque;
    public int ContentVersion;
    public bool Input;
}
