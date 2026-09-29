using System.Numerics;
using Basin.Diagnostics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using BepuBox = BepuPhysics.Collidables.Box;

namespace Basin.Physics;

internal sealed class BepuBackend : IPhysicsBackend
{
    public const float Depth = 100f;

    private const int VelocityIterations = 2;

    private const float SleepThreshold = 0.0025f;

    private const byte SleepSteps = 30;

    private const float WallFriction = 0.3f;

    private const float WallRestitution = 0.2f;

    private readonly BufferPool _pool = new();
    private readonly Simulation _simulation;
    private readonly BepuState _state = new();
    private BodyHandle[] _bodies = new BodyHandle[32];
    private TypedIndex[] _shapes = new TypedIndex[32];
    private PhysicsBodyKind[] _kinds = new PhysicsBodyKind[32];
    private float[] _halfWidths = new float[32];
    private float[] _halfHeights = new float[32];
    private float[] _densities = new float[32];
    private bool[] _walls = new bool[32];
    private StaticHandle[] _statics = new StaticHandle[16];
    private int[] _staticOwners = new int[16];
    private int _staticCount;

    public BepuBackend(double gravityX, double gravityY, double impactThreshold)
    {
        _state.GravityX = (float)gravityX;
        _state.GravityY = (float)gravityY;
        _state.ImpactThreshold = (float)impactThreshold;
        _simulation = Simulation.Create(
            _pool,
            new BepuNarrowPhase { State = _state },
            new BepuIntegrator { State = _state },
            new SolveDescription(VelocityIterations, PhysicsWorld.SubSteps));
    }

    public ReadOnlySpan<PhysicsHit> Hits => _state.Hits;

    public void SetGravity(double x, double y)
    {
        _state.GravityX = (float)x;
        _state.GravityY = (float)y;
    }

    public void SetImpactThreshold(double speed) => _state.ImpactThreshold = (float)speed;

    public void CreateBody(int slot, PhysicsBodyKind kind, double x, double y, double halfWidth, double halfHeight, in PhysicsMaterial material, bool awake)
    {
        Reserve(slot);
        _kinds[slot] = kind;
        _halfWidths[slot] = (float)halfWidth;
        _halfHeights[slot] = (float)halfHeight;
        _densities[slot] = (float)material.Density;
        _walls[slot] = false;

        BodyHandle handle;
        AllocationScope.Pause();
        try
        {
            var shape = _simulation.Shapes.Add(new BepuBox(2 * (float)halfWidth, 2 * (float)halfHeight, Depth));
            _shapes[slot] = shape;
            var pose = new RigidPose(new Vector3((float)x, (float)y, 0));
            var collidable = new CollidableDescription(shape, ContinuousDetection.Continuous());
            var activity = new BodyActivityDescription(SleepThreshold, SleepSteps);
            var description = kind == PhysicsBodyKind.Dynamic
                ? BodyDescription.CreateDynamic(pose, InertiaOf(slot, PhysicsLocks.None), collidable, activity)
                : BodyDescription.CreateKinematic(pose, collidable, activity);
            handle = _simulation.Bodies.Add(description);
            _bodies[slot] = handle;
            _state.EnsureBody(handle.Value);
        }
        finally
        {
            AllocationScope.Resume();
        }

        var index = handle.Value;
        _state.BodySlot[index] = slot;
        _state.Reach[index] = Reach(halfWidth, halfHeight);
        _state.BodyMask[index] = PhysicsCategory.All;
        _state.Locks[index] = PhysicsLocks.None;
        _state.DepthStamp[index] = 0;
        Apply(index, material);
        if (!awake || kind == PhysicsBodyKind.Static)
        {
            Sleep(handle);
        }
    }

    public void DestroyBody(int slot)
    {
        AllocationScope.Pause();
        try
        {
            if (_walls[slot])
            {
                RemoveWall(slot);
                return;
            }

            var handle = _bodies[slot];
            _state.BodySlot[handle.Value] = -1;
            _simulation.Bodies.Remove(handle);
            _simulation.Shapes.RemoveAndDispose(_shapes[slot], _pool);
        }
        finally
        {
            AllocationScope.Resume();
        }
    }

    public void SetKind(int slot, PhysicsBodyKind kind, PhysicsLocks locks)
    {
        if (_kinds[slot] == kind)
        {
            return;
        }

        _kinds[slot] = kind;
        var handle = _bodies[slot];
        var body = _simulation.Bodies[handle];
        AllocationScope.Pause();
        try
        {
            body.SetLocalInertia(kind == PhysicsBodyKind.Dynamic ? InertiaOf(slot, locks) : default);
        }
        finally
        {
            AllocationScope.Resume();
        }

        if (kind == PhysicsBodyKind.Static)
        {
            body.Velocity = default;
            Sleep(handle);
        }
    }

    public void SetLocks(int slot, PhysicsLocks locks)
    {
        var handle = _bodies[slot];
        _state.Locks[handle.Value] = locks;
        if (_kinds[slot] == PhysicsBodyKind.Dynamic)
        {
            _simulation.Bodies[handle].LocalInertia = InertiaOf(slot, locks);
        }
    }

    public void SetMaterial(int slot, in PhysicsMaterial material)
    {
        var handle = _bodies[slot];
        _densities[slot] = (float)material.Density;
        Apply(handle.Value, material);
        if (_kinds[slot] == PhysicsBodyKind.Dynamic)
        {
            _simulation.Bodies[handle].LocalInertia = InertiaOf(slot, _state.Locks[handle.Value]);
        }
    }

    public void SetMask(int slot, PhysicsCategory mask) => _state.BodyMask[_bodies[slot].Value] = mask;

    public void SetBox(int slot, double halfWidth, double halfHeight)
    {
        var handle = _bodies[slot];
        _halfWidths[slot] = (float)halfWidth;
        _halfHeights[slot] = (float)halfHeight;
        AllocationScope.Pause();
        try
        {
            var shape = _simulation.Shapes.Add(new BepuBox(2 * (float)halfWidth, 2 * (float)halfHeight, Depth));
            _simulation.Bodies[handle].SetShape(shape);
            _simulation.Shapes.RemoveAndDispose(_shapes[slot], _pool);
            _shapes[slot] = shape;
        }
        finally
        {
            AllocationScope.Resume();
        }

        _state.Reach[handle.Value] = Reach(halfWidth, halfHeight);

        if (_kinds[slot] == PhysicsBodyKind.Dynamic)
        {
            _simulation.Bodies[handle].LocalInertia = InertiaOf(slot, _state.Locks[handle.Value]);
        }
    }

    public double MassOf(int slot)
    {
        var inverse = _simulation.Bodies[_bodies[slot]].LocalInertia.InverseMass;
        return inverse == 0 ? 0 : 1.0 / inverse;
    }

    public void SetTransform(int slot, double x, double y, double angle)
    {
        var handle = _bodies[slot];
        ref var pose = ref _simulation.Bodies[handle].Pose;
        pose.Position = new Vector3((float)x, (float)y, 0);
        pose.Orientation = OrientationOf(angle);
        _simulation.Bodies.UpdateBounds(handle);
    }

    public void SetTarget(int slot, double x, double y, double angle, double seconds)
    {
        var handle = _bodies[slot];
        var body = _simulation.Bodies[handle];
        ref var pose = ref body.Pose;
        var vx = (float)((x - pose.Position.X) / seconds);
        var vy = (float)((y - pose.Position.Y) / seconds);
        var spin = (float)(Math.IEEERemainder(angle - AngleOf(pose.Orientation), Math.Tau) / seconds);
        if (vx != 0 || vy != 0 || spin != 0)
        {
            Wake(handle);
        }

        ref var velocity = ref _simulation.Bodies[handle].Velocity;
        velocity.Linear = new Vector3(vx, vy, 0);
        velocity.Angular = new Vector3(0, 0, spin);
    }

    public void ReadPose(int slot, out double x, out double y, out double angle)
    {
        ref var pose = ref _simulation.Bodies[_bodies[slot]].Pose;
        x = pose.Position.X;
        y = pose.Position.Y;
        angle = AngleOf(pose.Orientation);
    }

    public (double X, double Y) VelocityOf(int slot)
    {
        ref var velocity = ref _simulation.Bodies[_bodies[slot]].Velocity;
        return (velocity.Linear.X, velocity.Linear.Y);
    }

    public void SetVelocity(int slot, double x, double y)
    {
        if (_kinds[slot] == PhysicsBodyKind.Static)
        {
            return;
        }

        var handle = _bodies[slot];
        if (x != 0 || y != 0)
        {
            Wake(handle);
        }

        _simulation.Bodies[handle].Velocity.Linear = new Vector3((float)x, (float)y, 0);
    }

    public double AngularVelocityOf(int slot) => _simulation.Bodies[_bodies[slot]].Velocity.Angular.Z;

    public void SetAngularVelocity(int slot, double radiansPerSecond)
    {
        if (_kinds[slot] == PhysicsBodyKind.Static)
        {
            return;
        }

        var handle = _bodies[slot];
        if (radiansPerSecond != 0)
        {
            Wake(handle);
        }

        _simulation.Bodies[handle].Velocity.Angular = new Vector3(0, 0, (float)radiansPerSecond);
    }

    public bool IsAwake(int slot) => _simulation.Bodies[_bodies[slot]].Awake;

    public void SetAwake(int slot, bool awake)
    {
        var handle = _bodies[slot];
        if (awake)
        {
            Wake(handle);
            return;
        }

        var location = _simulation.Bodies.HandleToLocation[handle.Value];
        if (location.SetIndex != 0)
        {
            return;
        }

        if (_simulation.Bodies.ActiveSet.Constraints[location.Index].Count == 0)
        {
            Sleep(handle);
            return;
        }

        ref var activity = ref _simulation.Bodies[handle].Activity;
        activity.TimestepsUnderThresholdCount = activity.MinimumTimestepsUnderThreshold;
        activity.SleepCandidate = true;
    }

    public bool Penetrating(int slot, double depth)
    {
        var handle = _bodies[slot].Value;
        return _state.DepthStamp[handle] == _state.Stamp && _state.Depth[handle] > depth;
    }

    public void Step(double seconds, int subSteps)
    {
        _state.BeginStep((float)seconds);
        _simulation.Timestep((float)seconds);
    }

    public void CreateWall(int slot)
    {
        Reserve(slot);
        _walls[slot] = true;
        _kinds[slot] = PhysicsBodyKind.Static;
    }

    public void AddWallBox(int slot, double x, double y, double halfWidth, double halfHeight, PhysicsCategory category)
    {
        AllocationScope.Pause();
        try
        {
            var shape = _simulation.Shapes.Add(new BepuBox(2 * (float)halfWidth, 2 * (float)halfHeight, Depth));
            var handle = _simulation.Statics.Add(new StaticDescription(new Vector3((float)x, (float)y, 0), shape));
            _state.EnsureStatic(handle.Value);
            if (_staticCount == _statics.Length)
            {
                Array.Resize(ref _statics, _statics.Length * 2);
                Array.Resize(ref _staticOwners, _staticOwners.Length * 2);
            }

            _statics[_staticCount] = handle;
            _staticOwners[_staticCount++] = slot;
            _state.StaticSlot[handle.Value] = slot;
            _state.StaticCategory[handle.Value] = category;
            _state.StaticFriction[handle.Value] = WallFriction;
            _state.StaticRestitution[handle.Value] = WallRestitution;
        }
        finally
        {
            AllocationScope.Resume();
        }
    }

    public void Dispose()
    {
        _simulation.Dispose();
        _pool.Clear();
    }

    private void RemoveWall(int slot)
    {
        var write = 0;
        for (var read = 0; read < _staticCount; read++)
        {
            var handle = _statics[read];
            if (_staticOwners[read] != slot)
            {
                _statics[write] = handle;
                _staticOwners[write++] = _staticOwners[read];
                continue;
            }

            var shape = _simulation.Statics[handle].Shape;
            _simulation.Statics.Remove(handle);
            _simulation.Shapes.RemoveAndDispose(shape, _pool);
        }

        _staticCount = write;
        _walls[slot] = false;
    }

    private void Apply(int handle, in PhysicsMaterial material)
    {
        _state.GravityScale[handle] = (float)material.GravityScale;
        _state.Damping[handle] = (float)material.LinearDamping;
        _state.Friction[handle] = (float)material.Friction;
        _state.Restitution[handle] = (float)Math.Clamp(material.Restitution, 0, 1);
    }

    private void Wake(BodyHandle handle)
    {
        var body = _simulation.Bodies[handle];
        if (!body.Awake)
        {
            body.Awake = true;
        }

        ref var activity = ref _simulation.Bodies[handle].Activity;
        activity.TimestepsUnderThresholdCount = 0;
        activity.SleepCandidate = false;
    }

    private void Sleep(BodyHandle handle)
    {
        var body = _simulation.Bodies[handle];
        if (body.Awake)
        {
            body.Awake = false;
        }
    }

    private BodyInertia InertiaOf(int slot, PhysicsLocks locks)
    {
        var width = 2 * _halfWidths[slot];
        var height = 2 * _halfHeights[slot];
        var mass = _densities[slot] * width * height;
        if (mass <= 0)
        {
            mass = 1;
        }

        var inertia = mass * ((width * width) + (height * height)) / 12;
        var result = default(BodyInertia);
        result.InverseMass = 1 / mass;
        result.InverseInertiaTensor.ZZ = (locks & PhysicsLocks.Rotation) != 0 ? 0 : 1 / inertia;
        return result;
    }

    private void Reserve(int slot)
    {
        if (slot < _bodies.Length)
        {
            return;
        }

        var length = _bodies.Length;
        while (length <= slot)
        {
            length *= 2;
        }

        Array.Resize(ref _bodies, length);
        Array.Resize(ref _shapes, length);
        Array.Resize(ref _kinds, length);
        Array.Resize(ref _halfWidths, length);
        Array.Resize(ref _halfHeights, length);
        Array.Resize(ref _densities, length);
        Array.Resize(ref _walls, length);
    }

    private static float Reach(double halfWidth, double halfHeight) =>
        (float)Math.Sqrt((halfWidth * halfWidth) + (halfHeight * halfHeight));

    private static Quaternion OrientationOf(double angle) =>
        angle == 0 ? Quaternion.Identity : new Quaternion(0, 0, (float)Math.Sin(angle / 2), (float)Math.Cos(angle / 2));

    private static double AngleOf(Quaternion orientation) =>
        Math.IEEERemainder(2 * Math.Atan2(orientation.Z, orientation.W), Math.Tau);
}
