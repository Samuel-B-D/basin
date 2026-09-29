namespace Basin.Render.Gl;

public readonly record struct GlTextureView(
    uint Name, uint Target, int Width, int Height, bool Premultiplied, bool External, bool HasAlpha);
