using System.Numerics;
using Basin.Diagnostics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuUtilities.Memory;
using BepuBox = BepuPhysics.Collidables.Box;

namespace Basin.Physics;

internal sealed class Bepu3DBackend : IPhysicsBackend3D
{
    private const int VelocityIterations = 8;
    private const int SubSteps = 1;
    private const float SleepThreshold = 0.01f;
    private const byte SleepSteps = 32;
    private const float CharacterMargin = 0.1f;
    private const float MaximumSlope = MathF.PI * 0.3f;

    private readonly BufferPool _pool = new();
    private readonly Simulation _simulation;
    private readonly Bepu3DState _state = new();
    private readonly CharacterControllers _characters;
    private BodyHandle[] _bodies = new BodyHandle[32];
    private TypedIndex[] _shapes = new TypedIndex[32];
    private bool[] _isCharacter = new bool[32];
    private ConstraintHandle[] _grabLinear = new ConstraintHandle[32];
    private ConstraintHandle[] _grabAngular = new ConstraintHandle[32];
    private bool[] _grabbing = new bool[32];
    private Vector3[] _grabLocal = new Vector3[32];
    private readonly Dictionary<int, (StaticHandle Handle, TypedIndex Shape, bool Mesh)> _statics = [];
    private int _nextStatic = 1;

    public Bepu3DBackend(Vector3 gravity, float impactThreshold)
    {
        _state.Gravity = gravity;
        _state.ImpactThreshold = impactThreshold;
        _characters = new CharacterControllers(_pool, 8, 128);
        _simulation = Simulation.Create(
            _pool,
            new Bepu3DNarrowPhase { State = _state, Characters = _characters },
            new Bepu3DIntegrator { State = _state },
            new SolveDescription(VelocityIterations, SubSteps));
    }

    public ReadOnlySpan<PhysicsHit3D> Hits => _state.Hits;

    public void SetGravity(Vector3 gravity) => _state.Gravity = gravity;

    public void SetImpactThreshold(float speed) => _state.ImpactThreshold = speed;

    public int AddStaticBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, in PhysicsMaterial material)
    {
        AllocationScope.Pause();
        try
        {
            var shape = _simulation.Shapes.Add(new BepuBox(2 * halfExtents.X, 2 * halfExtents.Y, 2 * halfExtents.Z));
            var handle = _simulation.Statics.Add(new StaticDescription(new RigidPose(center, orientation), shape));
            return RegisterStatic(handle, shape, mesh: false, material);
        }
        finally
        {
            AllocationScope.Resume();
        }
    }

    public int AddStaticMesh(ReadOnlySpan<Vector3> vertices, ReadOnlySpan<int> indices, in PhysicsMaterial material)
    {
        AllocationScope.Pause();
        try
        {
            var count = indices.Length / 3;
            _pool.Take<Triangle>(count, out var triangles);
            for (var i = 0; i < count; i++)
            {
                triangles[i] = new Triangle(vertices[indices[i * 3]], vertices[indices[(i * 3) + 1]], vertices[indices[(i * 3) + 2]]);
            }

            var mesh = new Mesh(triangles, Vector3.One, _pool);
            var shape = _simulation.Shapes.Add(mesh);
            var handle = _simulation.Statics.Add(new StaticDescription(RigidPose.Identity, shape));
            return RegisterStatic(handle, shape, mesh: true, material);
        }
        finally
        {
            AllocationScope.Resume();
        }
    }

    public void RemoveStatic(int id)
    {
        if (!_statics.Remove(id, out var entry))
        {
            return;
        }

        AllocationScope.Pause();
        try
        {
            _simulation.Statics.Remove(entry.Handle);
            _simulation.Shapes.RemoveAndDispose(entry.Shape, _pool);
        }
        finally
        {
            AllocationScope.Resume();
        }
    }

    public void CreateBody(int slot, PhysicsShape3D shape, Vector3 halfExtents, in PhysicsPose3D pose, in PhysicsMaterial material, bool awake)
    {
        Reserve(slot);
        BodyHandle handle;
        AllocationScope.Pause();
        try
        {
            TypedIndex index;
            BodyInertia inertia;
            if (shape == PhysicsShape3D.Cylinder)
            {
                var cylinder = new Cylinder(halfExtents.X, 2 * halfExtents.Y);
                var mass = (float)material.Density * MathF.PI * halfExtents.X * halfExtents.X * 2 * halfExtents.Y;
                inertia = cylinder.ComputeInertia(MathF.Max(mass, 0.05f));
                index = _simulation.Shapes.Add(cylinder);
            }
            else
            {
                var box = new BepuBox(2 * halfExtents.X, 2 * halfExtents.Y, 2 * halfExtents.Z);
                var mass = (float)material.Density * 8 * halfExtents.X * halfExtents.Y * halfExtents.Z;
                inertia = box.ComputeInertia(MathF.Max(mass, 0.05f));
                index = _simulation.Shapes.Add(box);
            }

            _shapes[slot] = index;
            var description = BodyDescription.CreateDynamic(
                new RigidPose(pose.Position, pose.Orientation), inertia,
                new CollidableDescription(index, ContinuousDetection.Continuous()),
                new BodyActivityDescription(SleepThreshold, SleepSteps));
            handle = _simulation.Bodies.Add(description);
            _bodies[slot] = handle;
            _isCharacter[slot] = false;
            _state.EnsureBody(handle.Value);
        }
        finally
        {
            AllocationScope.Resume();
        }

        var at = handle.Value;
        _state.BodySlot[at] = slot;
        _state.Character[at] = false;
        Apply(at, material);
        if (!awake)
        {
            var body = _simulation.Bodies[handle];
            body.Awake = false;
        }
    }

    public void CreateCharacter(int slot, float radius, float height, Vector3 position, float mass, float jumpSpeed)
    {
        Reserve(slot);
        AllocationScope.Pause();
        try
        {
            var capsule = new Capsule(radius, MathF.Max(0.01f, height - (2 * radius)));
            var index = _simulation.Shapes.Add(capsule);
            _shapes[slot] = index;
            var handle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
                position,
                new BodyInertia { InverseMass = 1f / mass },
                new CollidableDescription(index, CharacterMargin, float.MaxValue, ContinuousDetection.Passive),
                radius * 0.02f));
            _bodies[slot] = handle;
            _isCharacter[slot] = true;
            _state.EnsureBody(handle.Value);
            _state.BodySlot[handle.Value] = slot;
            _state.Character[handle.Value] = true;
            _state.GravityScale[handle.Value] = 1;
            _state.Damping[handle.Value] = 0;
            _state.Friction[handle.Value] = 1;
            _state.Restitution[handle.Value] = 0;
            ref var character = ref _characters.AllocateCharacter(handle);
            character.LocalUp = Vector3.UnitY;
            character.CosMaximumSlope = MathF.Cos(MaximumSlope);
            character.JumpVelocity = jumpSpeed;
            character.MaximumVerticalForce = 100 * mass;
            character.MaximumHorizontalForce = 20 * mass;
            character.MinimumSupportDepth = radius * -0.01f;
            character.MinimumSupportContinuationDepth = -CharacterMargin;
            character.ViewDirection = -Vector3.UnitZ;
        }
        finally
        {
            AllocationScope.Resume();
        }
    }

    public void DestroyBody(int slot)
    {
        AllocationScope.Pause();
        try
        {
            if (_grabbing[slot])
            {
                EndGrab(slot);
            }

            var handle = _bodies[slot];
            if (_isCharacter[slot])
            {
                _characters.RemoveCharacterByBodyHandle(handle);
            }

            _state.BodySlot[handle.Value] = -1;
            _simulation.Bodies.Remove(handle);
            _simulation.Shapes.RemoveAndDispose(_shapes[slot], _pool);
        }
        finally
        {
            AllocationScope.Resume();
        }
    }

    public void ReadPose(int slot, out PhysicsPose3D pose)
    {
        ref var bodyPose = ref _simulation.Bodies[_bodies[slot]].Pose;
        pose = new PhysicsPose3D(bodyPose.Position, bodyPose.Orientation);
    }

    public void SetPose(int slot, in PhysicsPose3D pose)
    {
        var handle = _bodies[slot];
        ref var bodyPose = ref _simulation.Bodies[handle].Pose;
        bodyPose.Position = pose.Position;
        bodyPose.Orientation = pose.Orientation;
        _simulation.Bodies.UpdateBounds(handle);
    }

    public void ReadVelocity(int slot, out Vector3 linear, out Vector3 angular)
    {
        ref var velocity = ref _simulation.Bodies[_bodies[slot]].Velocity;
        linear = velocity.Linear;
        angular = velocity.Angular;
    }

    public void SetVelocity(int slot, Vector3 linear, Vector3 angular)
    {
        var handle = _bodies[slot];
        if (linear != Vector3.Zero || angular != Vector3.Zero)
        {
            Wake(handle);
        }

        ref var velocity = ref _simulation.Bodies[handle].Velocity;
        velocity.Linear = linear;
        velocity.Angular = _isCharacter[slot] ? Vector3.Zero : angular;
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
        if (location.SetIndex == 0 && _simulation.Bodies.ActiveSet.Constraints[location.Index].Count == 0)
        {
            var body = _simulation.Bodies[handle];
            body.Awake = false;
            return;
        }

        ref var activity = ref _simulation.Bodies[handle].Activity;
        activity.TimestepsUnderThresholdCount = activity.MinimumTimestepsUnderThreshold;
        activity.SleepCandidate = true;
    }

    public double MassOf(int slot)
    {
        var inverse = _simulation.Bodies[_bodies[slot]].LocalInertia.InverseMass;
        return inverse == 0 ? 0 : 1.0 / inverse;
    }

    public void SetCharacterGoal(int slot, Vector2 targetVelocity, Vector3 view, bool jump)
    {
        if (!_isCharacter[slot])
        {
            return;
        }

        var handle = _bodies[slot];
        ref var character = ref _characters.GetCharacterByBodyHandle(handle);
        character.TargetVelocity = targetVelocity;
        if (view.LengthSquared() > 1e-6f)
        {
            character.ViewDirection = Vector3.Normalize(view);
        }

        character.TryJump |= jump;
        Wake(handle);
    }

    public bool IsSupported(int slot) =>
        _isCharacter[slot] && _characters.GetCharacterByBodyHandle(_bodies[slot]).Supported;

    public void BeginGrab(int slot, Vector3 localPoint, Vector3 target)
    {
        if (_grabbing[slot])
        {
            EndGrab(slot);
        }

        var handle = _bodies[slot];
        var body = _simulation.Bodies[handle];
        var mass = 1f / MathF.Max(body.LocalInertia.InverseMass, 1e-4f);
        _grabLocal[slot] = localPoint;
        AllocationScope.Pause();
        try
        {
            _grabLinear[slot] = _simulation.Solver.Add(handle, new OneBodyLinearServo
            {
                LocalOffset = localPoint,
                Target = target,
                ServoSettings = new ServoSettings(float.MaxValue, 0, mass * 400),
                SpringSettings = new SpringSettings(6, 1.4f),
            });
            _grabAngular[slot] = _simulation.Solver.Add(handle, new OneBodyAngularServo
            {
                TargetOrientation = body.Pose.Orientation,
                ServoSettings = new ServoSettings(float.MaxValue, 0, mass * 40),
                SpringSettings = new SpringSettings(3, 1),
            });
        }
        finally
        {
            AllocationScope.Resume();
        }

        _grabbing[slot] = true;
        Wake(handle);
    }

    public void MoveGrab(int slot, Vector3 target)
    {
        if (!_grabbing[slot])
        {
            return;
        }

        var handle = _bodies[slot];
        var body = _simulation.Bodies[handle];
        var mass = 1f / MathF.Max(body.LocalInertia.InverseMass, 1e-4f);
        _simulation.Solver.ApplyDescription(_grabLinear[slot], new OneBodyLinearServo
        {
            LocalOffset = _grabLocal[slot],
            Target = target,
            ServoSettings = new ServoSettings(float.MaxValue, 0, mass * 400),
            SpringSettings = new SpringSettings(6, 1.4f),
        });
        Wake(handle);
    }

    public void TurnGrab(int slot, Quaternion orientation)
    {
        if (!_grabbing[slot])
        {
            return;
        }

        var handle = _bodies[slot];
        var body = _simulation.Bodies[handle];
        var mass = 1f / MathF.Max(body.LocalInertia.InverseMass, 1e-4f);
        _simulation.Solver.ApplyDescription(_grabAngular[slot], new OneBodyAngularServo
        {
            TargetOrientation = orientation,
            ServoSettings = new ServoSettings(float.MaxValue, 0, mass * 40),
            SpringSettings = new SpringSettings(3, 1),
        });
        Wake(handle);
    }

    public void EndGrab(int slot)
    {
        if (!_grabbing[slot])
        {
            return;
        }

        AllocationScope.Pause();
        try
        {
            _simulation.Solver.Remove(_grabLinear[slot]);
            _simulation.Solver.Remove(_grabAngular[slot]);
        }
        finally
        {
            AllocationScope.Resume();
        }

        _grabbing[slot] = false;
        Wake(_bodies[slot]);
    }

    public void Step(float seconds)
    {
        _state.BeginStep(seconds);
        _simulation.Timestep(seconds);
    }

    public void Dispose()
    {
        _simulation.Dispose();
        _pool.Clear();
    }

    private int RegisterStatic(StaticHandle handle, TypedIndex shape, bool mesh, in PhysicsMaterial material)
    {
        _state.EnsureStatic(handle.Value);
        _state.StaticSlot[handle.Value] = -1;
        _state.StaticFriction[handle.Value] = (float)material.Friction;
        _state.StaticRestitution[handle.Value] = (float)Math.Clamp(material.Restitution, 0, 1);
        var id = _nextStatic++;
        _statics[id] = (handle, shape, mesh);
        return id;
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
        Array.Resize(ref _isCharacter, length);
        Array.Resize(ref _grabLinear, length);
        Array.Resize(ref _grabAngular, length);
        Array.Resize(ref _grabbing, length);
        Array.Resize(ref _grabLocal, length);
    }
}
