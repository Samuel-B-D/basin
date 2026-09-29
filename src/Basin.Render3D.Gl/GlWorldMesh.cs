using Basin.Scene3D;
using Silk.NET.OpenGLES;

namespace Basin.Render3D.Gl;

internal sealed unsafe class GlWorldMesh
{
    private GlWorldMesh(uint vao, uint vertices, uint indices, int indexCount)
    {
        Vao = vao;
        VertexBuffer = vertices;
        IndexBuffer = indices;
        IndexCount = indexCount;
    }

    public uint Vao { get; }

    public uint VertexBuffer { get; }

    public uint IndexBuffer { get; }

    public int IndexCount { get; }

    public long LastUsed { get; set; }

    public static GlWorldMesh Create(GL gl, WorldMesh mesh)
    {
        var vao = gl.GenVertexArray();
        gl.BindVertexArray(vao);
        var vertexBuffer = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        fixed (WorldVertex* data = mesh.Vertices)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(mesh.Vertices.Length * sizeof(WorldVertex)), data, BufferUsageARB.StaticDraw);
        }

        var stride = (uint)sizeof(WorldVertex);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)12);
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)24);
        var indexBuffer = UploadIndices(gl, mesh.Indices);
        gl.BindVertexArray(0);
        return new GlWorldMesh(vao, vertexBuffer, indexBuffer, mesh.Indices.Length);
    }

    public static GlWorldMesh Create(GL gl, SkinnedMesh mesh)
    {
        var vao = gl.GenVertexArray();
        gl.BindVertexArray(vao);
        var vertexBuffer = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        fixed (SkinnedVertex* data = mesh.Vertices)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(mesh.Vertices.Length * sizeof(SkinnedVertex)), data, BufferUsageARB.StaticDraw);
        }

        var stride = (uint)sizeof(SkinnedVertex);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)12);
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)24);
        gl.EnableVertexAttribArray(3);
        gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, stride, (void*)32);
        gl.EnableVertexAttribArray(4);
        gl.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, stride, (void*)48);
        var indexBuffer = UploadIndices(gl, mesh.Indices);
        gl.BindVertexArray(0);
        return new GlWorldMesh(vao, vertexBuffer, indexBuffer, mesh.Indices.Length);
    }

    public void Draw(GL gl)
    {
        gl.BindVertexArray(Vao);
        gl.DrawElements(PrimitiveType.Triangles, (uint)IndexCount, DrawElementsType.UnsignedInt, (void*)0);
    }

    public void Dispose(GL gl)
    {
        gl.DeleteVertexArray(Vao);
        gl.DeleteBuffer(VertexBuffer);
        gl.DeleteBuffer(IndexBuffer);
    }

    private static uint UploadIndices(GL gl, uint[] indices)
    {
        var buffer = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, buffer);
        fixed (uint* data = indices)
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), data, BufferUsageARB.StaticDraw);
        }

        return buffer;
    }
}
