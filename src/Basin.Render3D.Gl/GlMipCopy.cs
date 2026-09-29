using Silk.NET.OpenGLES;

namespace Basin.Render3D.Gl;

internal sealed class GlMipCopy
{
    private GlMipCopy(uint texture, uint framebuffer, int width, int height)
    {
        Texture = texture;
        Framebuffer = framebuffer;
        Width = width;
        Height = height;
    }

    public uint Texture { get; }

    public uint Framebuffer { get; }

    public int Width { get; }

    public int Height { get; }

    public ITexture? Source { get; set; }

    public int Version { get; set; } = -1;

    public static GlMipCopy Create(GL gl, int width, int height)
    {
        var levels = 1 + (int)Math.Floor(Math.Log2(Math.Max(width, height)));
        var texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.TexStorage2D(TextureTarget.Texture2D, (uint)levels, SizedInternalFormat.Rgba8, (uint)width, (uint)height);
        var framebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
        return new GlMipCopy(texture, framebuffer, width, height);
    }

    public void Dispose(GL gl)
    {
        gl.DeleteFramebuffer(Framebuffer);
        gl.DeleteTexture(Texture);
    }
}
