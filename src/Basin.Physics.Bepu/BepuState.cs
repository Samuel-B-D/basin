using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;

namespace Basin.Physics;

internal sealed class BepuState
{
    public const float DefaultRecovery = 2f;

    private const float TouchSlop = 0.005f;

    private const float MarginSlop = 0.01f;

    private Dictionary<ulong, float> _touching = new(64);
    private Dictionary<ulong, float> _touched = new(64);
    private PhysicsHit[] _hits = new PhysicsHit[16];
    private int _hitCount;
    private float _stepSeconds;

    public Simulation? Simulation;
    public float GravityX;
    public float GravityY;
    public float ImpactThreshold;
    public int Stamp;

    public int[] BodySlot = new int[32];
    public PhysicsCategory[] BodyMask = new PhysicsCategory[32];
    public float[] GravityScale = new float[32];
    public float[] Damping = new float[32];
    public float[] Friction = new float[32];
    public float[] Restitution = new float[32];
    public PhysicsLocks[] Locks = new PhysicsLocks[32];
    public float[] Reach = new float[32];
    public float[] Depth = new float[32];
    public int[] DepthStamp = new int[32];

    public int[] StaticSlot = new int[32];
    public PhysicsCategory[] StaticCategory = new PhysicsCategory[32];
    public float[] StaticFriction = new float[32];
    public float[] StaticRestitution = new float[32];

    public ReadOnlySpan<PhysicsHit> Hits => _hits.AsSpan(0, _hitCount);

    public void EnsureBody(int handle)
    {
        if (handle < BodySlot.Length)
        {
            return;
        }

        var length = Grow(BodySlot.Length, handle);
        Array.Resize(ref BodySlot, length);
        Array.Resize(ref BodyMask, length);
        Array.Resize(ref GravityScale, length);
        Array.Resize(ref Damping, length);
        Array.Resize(ref Friction, length);
        Array.Resize(ref Restitution, length);
        Array.Resize(ref Locks, length);
        Array.Resize(ref Reach, length);
        Array.Resize(ref Depth, length);
        Array.Resize(ref DepthStamp, length);
    }

    public void EnsureStatic(int handle)
    {
        if (handle < StaticSlot.Length)
        {
            return;
        }

        var length = Grow(StaticSlot.Length, handle);
        Array.Resize(ref StaticSlot, length);
        Array.Resize(ref StaticCategory, length);
        Array.Resize(ref StaticFriction, length);
        Array.Resize(ref StaticRestitution, length);
    }

    public void BeginStep(float seconds)
    {
        (_touched, _touching) = (_touching, _touched);
        _touching.Clear();
        _hitCount = 0;
        _stepSeconds = seconds;
        Stamp++;
    }

    public bool Collides(CollidableReference a, CollidableReference b)
    {
        Filter(a, out var categoryA, out var maskA);
        Filter(b, out var categoryB, out var maskB);
        return (categoryA & maskB) != 0 && (categoryB & maskA) != 0;
    }

    public float Margin(CollidableReference a, CollidableReference b)
    {
        var simulation = Simulation!;
        Motion(simulation, a, out var linearA, out var sweepA);
        Motion(simulation, b, out var linearB, out var sweepB);
        return (((linearA - linearB).Length() + sweepA + sweepB) * _stepSeconds) + MarginSlop;
    }

    public void Material(CollidableReference collidable, out float friction, out float restitution)
    {
        if (collidable.Mobility == CollidableMobility.Static)
        {
            var handle = collidable.StaticHandle.Value;
            friction = StaticFriction[handle];
            restitution = StaticRestitution[handle];
        }
        else
        {
            var handle = collidable.BodyHandle.Value;
            friction = Friction[handle];
            restitution = Restitution[handle];
        }
    }

    public float Track(CollidablePair pair, int count, Vector3 offset, Vector3 normal, float depth)
    {
        if (count == 0)
        {
            return 0;
        }

        RecordDepth(pair.A, depth);
        RecordDepth(pair.B, depth);
        var simulation = Simulation!;
        var positionA = PositionOf(simulation, pair.A);
        var point = positionA + offset;
        var velocityA = PointVelocity(simulation, pair.A, point);
        var velocityB = PointVelocity(simulation, pair.B, point);
        var approach = Vector3.Dot(velocityB - velocityA, normal);
        var key = Key(pair);
        if (_touched.TryGetValue(key, out var begin))
        {
            _touching[key] = begin;
            return begin;
        }

        if (depth < -TouchSlop && depth + (MathF.Max(approach, 0) * _stepSeconds) < 0)
        {
            return 0;
        }

        begin = MathF.Max(approach, 0);
        _touching[key] = begin;
        if (approach > ImpactThreshold)
        {
            if (_hitCount == _hits.Length)
            {
                Array.Resize(ref _hits, _hits.Length * 2);
            }

            _hits[_hitCount++] = new PhysicsHit(SlotOf(pair.A), SlotOf(pair.B), point.X, point.Y, -normal.X, -normal.Y, approach);
        }

        return begin;
    }

    private void RecordDepth(CollidableReference collidable, float depth)
    {
        if (collidable.Mobility == CollidableMobility.Static)
        {
            return;
        }

        var handle = collidable.BodyHandle.Value;
        if (DepthStamp[handle] != Stamp)
        {
            DepthStamp[handle] = Stamp;
            Depth[handle] = depth;
        }
        else if (depth > Depth[handle])
        {
            Depth[handle] = depth;
        }
    }

    private void Filter(CollidableReference collidable, out PhysicsCategory category, out PhysicsCategory mask)
    {
        if (collidable.Mobility == CollidableMobility.Static)
        {
            category = StaticCategory[collidable.StaticHandle.Value];
            mask = PhysicsCategory.Windows;
        }
        else
        {
            category = PhysicsCategory.Windows;
            mask = BodyMask[collidable.BodyHandle.Value];
        }
    }

    private int SlotOf(CollidableReference collidable) =>
        collidable.Mobility == CollidableMobility.Static
            ? StaticSlot[collidable.StaticHandle.Value]
            : BodySlot[collidable.BodyHandle.Value];

    private static Vector3 PositionOf(Simulation simulation, CollidableReference collidable) =>
        collidable.Mobility == CollidableMobility.Static
            ? simulation.Statics[collidable.StaticHandle].Pose.Position
            : simulation.Bodies[collidable.BodyHandle].Pose.Position;

    private static Vector3 PointVelocity(Simulation simulation, CollidableReference collidable, Vector3 point)
    {
        if (collidable.Mobility == CollidableMobility.Static)
        {
            return Vector3.Zero;
        }

        var body = simulation.Bodies[collidable.BodyHandle];
        ref var velocity = ref body.Velocity;
        var arm = point - body.Pose.Position;
        var spin = velocity.Angular.Z;
        return new Vector3(velocity.Linear.X - (spin * arm.Y), velocity.Linear.Y + (spin * arm.X), 0);
    }

    private void Motion(Simulation simulation, CollidableReference collidable, out Vector3 linear, out float sweep)
    {
        if (collidable.Mobility == CollidableMobility.Static)
        {
            linear = Vector3.Zero;
            sweep = 0;
            return;
        }

        ref var velocity = ref simulation.Bodies[collidable.BodyHandle].Velocity;
        linear = velocity.Linear;
        sweep = MathF.Abs(velocity.Angular.Z) * Reach[collidable.BodyHandle.Value];
    }

    private static ulong Key(CollidablePair pair)
    {
        var a = pair.A.Packed;
        var b = pair.B.Packed;
        return a < b ? ((ulong)a << 32) | b : ((ulong)b << 32) | a;
    }

    private static int Grow(int length, int handle)
    {
        while (length <= handle)
        {
            length *= 2;
        }

        return length;
    }
}
