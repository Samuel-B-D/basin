using System.Numerics;

namespace Basin.Physics;

internal interface IPhysicsBackend3D : IDisposable
{
    ReadOnlySpan<PhysicsHit3D> Hits { get; }

    void SetGravity(Vector3 gravity);

    void SetImpactThreshold(float speed);

    int AddStaticBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, in PhysicsMaterial material);

    int AddStaticMesh(ReadOnlySpan<Vector3> vertices, ReadOnlySpan<int> indices, in PhysicsMaterial material);

    void RemoveStatic(int id);

    void CreateBody(int slot, PhysicsShape3D shape, Vector3 halfExtents, in PhysicsPose3D pose, in PhysicsMaterial material, bool awake);

    void CreateCharacter(int slot, float radius, float height, Vector3 position, float mass, float jumpSpeed);

    void DestroyBody(int slot);

    void ReadPose(int slot, out PhysicsPose3D pose);

    void SetPose(int slot, in PhysicsPose3D pose);

    void ReadVelocity(int slot, out Vector3 linear, out Vector3 angular);

    void SetVelocity(int slot, Vector3 linear, Vector3 angular);

    bool IsAwake(int slot);

    void SetAwake(int slot, bool awake);

    double MassOf(int slot);

    void SetCharacterGoal(int slot, Vector2 targetVelocity, Vector3 view, bool jump);

    bool IsSupported(int slot);

    void BeginGrab(int slot, Vector3 localPoint, Vector3 target);

    void MoveGrab(int slot, Vector3 target);

    void TurnGrab(int slot, Quaternion orientation);

    void EndGrab(int slot);

    void Step(float seconds);
}
