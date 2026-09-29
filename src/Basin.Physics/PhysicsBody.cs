namespace Basin.Physics;

public readonly record struct PhysicsBody(PhysicsWorld? World, int Slot, int Generation)
{
    public bool IsValid => World is { } world && world.IsLive(this);

    public PhysicsBodyKind Kind
    {
        get => Live.KindOf(this);
        set => Live.SetKind(this, value);
    }

    public ulong Tag
    {
        get => Live.TagOf(this);
        set => Live.SetTag(this, value);
    }

    public double Width => Live.SizeOf(this).Width;

    public double Height => Live.SizeOf(this).Height;

    public PhysicsPose Pose => Live.Pose(this);

    public PhysicsPose Current => Live.CurrentPose(this);

    public (double X, double Y) Velocity
    {
        get => Live.VelocityOf(this);
        set => Live.SetVelocity(this, value.X, value.Y);
    }

    public double AngularVelocity
    {
        get => Live.AngularVelocityOf(this);
        set => Live.SetAngularVelocity(this, value);
    }

    public bool AngularLocked
    {
        get => (Live.LocksOf(this) & PhysicsLocks.Rotation) != 0;
        set => Live.SetLocks(this, value ? Live.LocksOf(this) | PhysicsLocks.Rotation : Live.LocksOf(this) & ~PhysicsLocks.Rotation);
    }

    public PhysicsLocks Locks
    {
        get => Live.LocksOf(this);
        set => Live.SetLocks(this, value);
    }

    public PhysicsMaterial Material
    {
        get => Live.MaterialOf(this);
        set => Live.SetMaterial(this, value);
    }

    public PhysicsCategory CollidesWith
    {
        get => Live.MaskOf(this);
        set => Live.SetMask(this, value);
    }

    public bool Upright
    {
        get => Live.UprightOf(this);
        set => Live.SetUpright(this, value);
    }

    public bool IsAwake => Live.IsBodyAwake(this);

    public bool IsDragging => Live.IsDragging(this);

    public bool IsUprighting => Live.IsUprighting(this);

    private PhysicsWorld Live => World ?? throw new InvalidOperationException("The body belongs to no world.");

    public void Teleport(double x, double y, double angle = 0) => Live.Teleport(this, x, y, angle);

    public void Resize(double width, double height) => Live.Resize(this, width, height);

    public void Destroy() => World?.Destroy(this);
}
