namespace Basin.Scene;

public interface ISceneInputFilter
{
    bool AcceptsInputAt(SceneBuffer buffer, double x, double y);
}
