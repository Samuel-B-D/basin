using Basin.Scene;

namespace Basin.Physics;

public static class PhysicsTransforms
{
    public const string Name = "physics";

    public static bool Apply(TransformStack stack, in PhysicsPose pose) => Apply(stack, pose, Name);

    public static bool Apply(TransformStack stack, in PhysicsPose pose, string name)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (pose.Angle == 0)
        {
            return stack.Remove(name);
        }

        var node = stack.Get(name) ?? stack.Add(TransformStack.ZOrder.Transform2D, name);
        var matrix = pose.Rotation;
        if (node.Matrix == matrix)
        {
            return false;
        }

        node.Matrix = matrix;
        return true;
    }
}
