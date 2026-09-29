namespace Basin.Physics;

internal interface IPhysicsBackend : IDisposable
{
    ReadOnlySpan<PhysicsHit> Hits { get; }

    void SetGravity(double x, double y);

    void SetImpactThreshold(double speed);

    void CreateBody(int slot, PhysicsBodyKind kind, double x, double y, double halfWidth, double halfHeight, in PhysicsMaterial material, bool awake);

    void DestroyBody(int slot);

    void SetKind(int slot, PhysicsBodyKind kind, PhysicsLocks locks);

    void SetLocks(int slot, PhysicsLocks locks);

    void SetMaterial(int slot, in PhysicsMaterial material);

    void SetMask(int slot, PhysicsCategory mask);

    void SetBox(int slot, double halfWidth, double halfHeight);

    double MassOf(int slot);

    void SetTransform(int slot, double x, double y, double angle);

    void SetTarget(int slot, double x, double y, double angle, double seconds);

    void ReadPose(int slot, out double x, out double y, out double angle);

    (double X, double Y) VelocityOf(int slot);

    void SetVelocity(int slot, double x, double y);

    double AngularVelocityOf(int slot);

    void SetAngularVelocity(int slot, double radiansPerSecond);

    bool IsAwake(int slot);

    void SetAwake(int slot, bool awake);

    bool Penetrating(int slot, double depth);

    void Step(double seconds, int subSteps);

    void CreateWall(int slot);

    void AddWallBox(int slot, double x, double y, double halfWidth, double halfHeight, PhysicsCategory category);
}
