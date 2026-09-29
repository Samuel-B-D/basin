using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;

namespace Basin.Physics;

internal sealed class Bepu3DState
{
    public const float DefaultRecovery = 2f;

    private const float TouchSlop = 0.005f;

    private Dictionary<ulong, float> _touching = new(128);
    private Dictionary<ulong, float> _touched = new(128);
    private PhysicsHit3D[] _hits = new PhysicsHit3D[16];
    private int _hitCount;
    private float _stepSeconds;

    public Simulation? Simulation;
    public Vector3 Gravity;
    public float ImpactThreshold;

    public int[] BodySlot = new int[64];
    public float[] GravityScale = new float[64];
    public float[] Damping = new float[64];
    public float[] Friction = new float[64];
    public float[] Restitution = new float[64];
    public bool[] Character = new bool[64];

    public int[] StaticSlot = new int[64];
    public float[] StaticFriction = new float[64];
    public float[] StaticRestitution = new float[64];

    public ReadOnlySpan<PhysicsHit3D> Hits => _hits.AsSpan(0, _hitCount);

    public void EnsureBody(int handle)
    {
        if (handle < BodySlot.Length)
        {
            return;
        }

        var length = Grow(BodySlot.Length, handle);
        Array.Resize(ref BodySlot, length);
        Array.Resize(ref GravityScale, length);
        Array.Resize(ref Damping, length);
        Array.Resize(ref Friction, length);
        Array.Resize(ref Restitution, length);
        Array.Resize(ref Character, length);
    }

    public void EnsureStatic(int handle)
    {
        if (handle < StaticSlot.Length)
        {
            return;
        }

        var length = Grow(StaticSlot.Length, handle);
        Array.Resize(ref StaticSlot, length);
        Array.Resize(ref StaticFriction, length);
        Array.Resize(ref StaticRestitution, length);
    }

    public void BeginStep(float seconds)
    {
        (_touched, _touching) = (_touching, _touched);
        _touching.Clear();
        _hitCount = 0;
        _stepSeconds = seconds;
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

        var simulation = Simulation!;
        var point = PositionOf(simulation, pair.A) + offset;
        var approach = Vector3.Dot(PointVelocity(simulation, pair.B, point) - PointVelocity(simulation, pair.A, point), normal);
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
        if (approach > ImpactThreshold && !IsCharacter(pair.A) && !IsCharacter(pair.B))
        {
            if (_hitCount == _hits.Length)
            {
                Array.Resize(ref _hits, _hits.Length * 2);
            }

            _hits[_hitCount++] = new PhysicsHit3D(SlotOf(pair.A), SlotOf(pair.B), point, -normal, approach);
        }

        return begin;
    }

    private bool IsCharacter(CollidableReference collidable) =>
        collidable.Mobility != CollidableMobility.Static && Character[collidable.BodyHandle.Value];

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
        return velocity.Linear + Vector3.Cross(velocity.Angular, point - body.Pose.Position);
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
