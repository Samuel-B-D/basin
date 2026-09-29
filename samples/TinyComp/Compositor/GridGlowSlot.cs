using Basin;
using Basin.Scene;

namespace TinyComp;

internal sealed class GridGlowSlot
{
    private readonly PixelShaderUniformValue[] _values;
    private readonly PixelShaderUniformValue[] _applied;
    private bool _fresh;

    public GridGlowSlot(int uniforms)
    {
        _values = new PixelShaderUniformValue[uniforms];
        _applied = new PixelShaderUniformValue[uniforms];
    }

    public IPixelShader? Shader { get; private set; }

    public SceneShader? Node { get; set; }

    public string State { get; set; } = "none";

    public Span<PixelShaderUniformValue> Values => _values;

    private bool _tried;

    public IPixelShader? Compile(IRenderer renderer, in PixelShaderSource source, ReadOnlySpan<PixelShaderUniform> uniforms, List<IPixelShader> owned)
    {
        if (!_tried)
        {
            _tried = true;
            Shader = renderer.CompilePixelShader(source, uniforms);
            if (Shader is not null)
            {
                owned.Add(Shader);
            }
        }

        return Shader;
    }

    public bool Live => Node is { IsDestroyed: false } && Shader is not null;

    public void Apply()
    {
        if (Node is not { IsDestroyed: false } node || Shader is not { } shader)
        {
            return;
        }

        if (_fresh && _values.AsSpan().SequenceEqual(_applied))
        {
            return;
        }

        shader.SetUniforms(_values);
        _values.AsSpan().CopyTo(_applied);
        _fresh = true;
        node.NotifyShaderChanged();
    }

    public bool Place(SceneTree parent, in Box box)
    {
        if (Shader is null)
        {
            return false;
        }

        var changed = false;
        if (Node is not { IsDestroyed: false })
        {
            Node = new SceneShader(parent) { Shader = Shader, Bounds = box };
            _fresh = false;
            changed = true;
        }

        changed |= Node.Bounds != box;
        Node.Bounds = box;
        return changed;
    }

    public bool DropNode()
    {
        if (Node is null)
        {
            return false;
        }

        Node.Destroy();
        Node = null;
        _fresh = false;
        return true;
    }

    public void Dispose(List<IPixelShader> owned)
    {
        DropNode();
        if (Shader is { } shader && owned.Remove(shader))
        {
            shader.Dispose();
        }

        Shader = null;
        _tried = false;
    }
}
