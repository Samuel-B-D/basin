using Basin.Render.Gl;
using Silk.NET.OpenGLES;

namespace Basin.Render3D.Gl;

internal sealed class GlWorldTarget
{
    private GlWorldTarget(GlRenderTarget target, uint depthStencil)
    {
        Target = target;
        DepthStencil = depthStencil;
    }

    public GlRenderTarget Target { get; }

    public uint DepthStencil { get; }

    public static GlWorldTarget Create(GlDevice device, IBuffer buffer)
    {
        var gl = device.Gl;
        var target = GlRenderTarget.Create(device, buffer);
        var depthStencil = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthStencil);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)buffer.Width, (uint)buffer.Height);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, target.Framebuffer);
        gl.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, depthStencil);
        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            gl.DeleteRenderbuffer(depthStencil);
            target.Dispose(device);
            throw new InvalidOperationException("world target framebuffer incomplete with depth and stencil");
        }

        return new GlWorldTarget(target, depthStencil);
    }

    public void Dispose(GlDevice device)
    {
        device.Gl.DeleteRenderbuffer(DepthStencil);
        Target.Dispose(device);
    }
}
