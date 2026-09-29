namespace Basin.Scene3D;

public interface IWorldRenderer : IDisposable
{
    string Name { get; }

    bool Render(in WorldFrame frame, IBuffer target);

    void Forget(WorldWindow window);
}
