using Basin.Diagnostics;
using Box2D.NET;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;
using static Box2D.NET.B2Worlds;

namespace Basin.Physics;

internal sealed class Box2DBackend : IPhysicsBackend
{
    private readonly B2WorldId _world;
    private readonly B2ContactData[] _contacts = new B2ContactData[16];
    private B2BodyId[] _bodies = new B2BodyId[32];
    private B2ShapeId[] _shapes = new B2ShapeId[32];
    private PhysicsHit[] _hits = new PhysicsHit[16];
    private int _hitCount;

    public Box2DBackend(double gravityX, double gravityY, double impactThreshold)
    {
        var def = b2DefaultWorldDef();
        def.gravity = new B2Vec2((float)gravityX, (float)gravityY);
        def.workerCount = 1;
        def.enableSleep = true;
        def.enableContinuous = true;
        def.hitEventThreshold = (float)impactThreshold;
        _world = b2CreateWorld(def);
    }

    public ReadOnlySpan<PhysicsHit> Hits => _hits.AsSpan(0, _hitCount);

    public void SetGravity(double x, double y) => b2World_SetGravity(_world, new B2Vec2((float)x, (float)y));

    public void SetImpactThreshold(double speed) => b2World_SetHitEventThreshold(_world, (float)speed);

    public void CreateBody(int slot, PhysicsBodyKind kind, double x, double y, double halfWidth, double halfHeight, in PhysicsMaterial material, bool awake)
    {
        Reserve(slot);
        var bodyDef = b2DefaultBodyDef();
        bodyDef.type = TypeOf(kind);
        bodyDef.position = new B2Vec2((float)x, (float)y);
        bodyDef.linearDamping = (float)material.LinearDamping;
        bodyDef.angularDamping = (float)(material.LinearDamping + 1);
        bodyDef.gravityScale = (float)material.GravityScale;
        bodyDef.userData = new B2UserData((long)slot);
        bodyDef.isAwake = awake;

        AllocationScope.Pause();
        try
        {
            _bodies[slot] = b2CreateBody(_world, bodyDef);
            var shapeDef = ShapeDef(material, PhysicsCategory.Windows, PhysicsCategory.All);
            var box = b2MakeBox((float)halfWidth, (float)halfHeight);
            _shapes[slot] = b2CreatePolygonShape(_bodies[slot], shapeDef, box);
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
            b2DestroyBody(_bodies[slot]);
        }
        finally
        {
            AllocationScope.Resume();
        }

        _bodies[slot] = default;
        _shapes[slot] = default;
    }

    public void SetKind(int slot, PhysicsBodyKind kind, PhysicsLocks locks)
    {
        var id = _bodies[slot];
        var type = TypeOf(kind);
        if (b2Body_GetType(id) == type)
        {
            return;
        }

        AllocationScope.Pause();
        try
        {
            b2Body_SetType(id, type);
        }
        finally
        {
            AllocationScope.Resume();
        }

        if (kind == PhysicsBodyKind.Dynamic)
        {
            SetLocks(slot, locks);
        }
    }

    public void SetLocks(int slot, PhysicsLocks locks) =>
        b2Body_SetMotionLocks(_bodies[slot], new B2MotionLocks(
            (locks & PhysicsLocks.X) != 0, (locks & PhysicsLocks.Y) != 0, (locks & PhysicsLocks.Rotation) != 0));

    public void SetMaterial(int slot, in PhysicsMaterial material)
    {
        var id = _bodies[slot];
        var shape = _shapes[slot];
        AllocationScope.Pause();
        try
        {
            b2Body_SetLinearDamping(id, (float)material.LinearDamping);
            b2Body_SetAngularDamping(id, (float)(material.LinearDamping + 1));
            b2Body_SetGravityScale(id, (float)material.GravityScale);
            b2Shape_SetFriction(shape, (float)material.Friction);
            b2Shape_SetRestitution(shape, (float)material.Restitution);
            b2Shape_SetDensity(shape, (float)material.Density, true);
        }
        finally
        {
            AllocationScope.Resume();
        }
    }

    public void SetMask(int slot, PhysicsCategory mask)
    {
        AllocationScope.Pause();
        try
        {
            b2Shape_SetFilter(_shapes[slot], new B2Filter((ulong)PhysicsCategory.Windows, (ulong)mask, 0));
        }
        finally
        {
            AllocationScope.Resume();
        }
    }

    public void SetBox(int slot, double halfWidth, double halfHeight)
    {
        var box = b2MakeBox((float)halfWidth, (float)halfHeight);
        AllocationScope.Pause();
        try
        {
            b2Shape_SetPolygon(_shapes[slot], ref box);
            b2Body_ApplyMassFromShapes(_bodies[slot]);
        }
        finally
        {
            AllocationScope.Resume();
        }
    }

    public double MassOf(int slot) => b2Body_GetMass(_bodies[slot]);

    public void SetTransform(int slot, double x, double y, double angle) =>
        b2Body_SetTransform(_bodies[slot], new B2Vec2((float)x, (float)y), RotationOf(angle));

    public void SetTarget(int slot, double x, double y, double angle, double seconds)
    {
        var target = new B2Transform(new B2Vec2((float)x, (float)y), RotationOf(angle));
        b2Body_SetTargetTransform(_bodies[slot], target, (float)seconds, true);
    }

    public void ReadPose(int slot, out double x, out double y, out double angle)
    {
        var id = _bodies[slot];
        var position = b2Body_GetPosition(id);
        var rotation = b2Body_GetRotation(id);
        x = position.X;
        y = position.Y;
        angle = Math.Atan2(rotation.s, rotation.c);
    }

    public (double X, double Y) VelocityOf(int slot)
    {
        var velocity = b2Body_GetLinearVelocity(_bodies[slot]);
        return (velocity.X, velocity.Y);
    }

    public void SetVelocity(int slot, double x, double y) =>
        b2Body_SetLinearVelocity(_bodies[slot], new B2Vec2((float)x, (float)y));

    public double AngularVelocityOf(int slot) => b2Body_GetAngularVelocity(_bodies[slot]);

    public void SetAngularVelocity(int slot, double radiansPerSecond) =>
        b2Body_SetAngularVelocity(_bodies[slot], (float)radiansPerSecond);

    public bool IsAwake(int slot) => b2Body_IsAwake(_bodies[slot]);

    public void SetAwake(int slot, bool awake) => b2Body_SetAwake(_bodies[slot], awake);

    public bool Penetrating(int slot, double depth)
    {
        var limit = -(float)depth;
        var count = b2Body_GetContactData(_bodies[slot], _contacts, _contacts.Length);
        for (var i = 0; i < count; i++)
        {
            ref readonly var manifold = ref _contacts[i].manifold;
            for (var p = 0; p < manifold.pointCount; p++)
            {
                if (manifold.points[p].separation < limit)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void Step(double seconds, int subSteps)
    {
        AllocationScope.Pause();
        try
        {
            b2World_Step(_world, (float)seconds, subSteps);
        }
        finally
        {
            AllocationScope.Resume();
        }

        _hitCount = 0;
        var events = b2World_GetContactEvents(_world);
        for (var i = 0; i < events.hitCount; i++)
        {
            ref readonly var hit = ref events.hitEvents[i];
            if (_hitCount == _hits.Length)
            {
                Array.Resize(ref _hits, _hits.Length * 2);
            }

            _hits[_hitCount++] = new PhysicsHit(
                SlotOf(hit.shapeIdA),
                SlotOf(hit.shapeIdB),
                hit.point.X,
                hit.point.Y,
                hit.normal.X,
                hit.normal.Y,
                hit.approachSpeed);
        }
    }

    public void CreateWall(int slot)
    {
        Reserve(slot);
        var bodyDef = b2DefaultBodyDef();
        bodyDef.type = B2BodyType.b2_staticBody;
        bodyDef.userData = new B2UserData((long)slot);
        AllocationScope.Pause();
        try
        {
            _bodies[slot] = b2CreateBody(_world, bodyDef);
        }
        finally
        {
            AllocationScope.Resume();
        }
    }

    public void AddWallBox(int slot, double x, double y, double halfWidth, double halfHeight, PhysicsCategory category)
    {
        var polygon = b2MakeOffsetBox((float)halfWidth, (float)halfHeight, new B2Vec2((float)x, (float)y), new B2Rot(1, 0));
        var shapeDef = ShapeDef(PhysicsMaterial.Default, category, PhysicsCategory.Windows);
        shapeDef.material.restitution = 0.2f;
        AllocationScope.Pause();
        try
        {
            b2CreatePolygonShape(_bodies[slot], shapeDef, polygon);
        }
        finally
        {
            AllocationScope.Resume();
        }
    }

    public void Dispose() => b2DestroyWorld(_world);

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
    }

    private static int SlotOf(B2ShapeId shape) => (int)b2Body_GetUserData(b2Shape_GetBody(shape)).GetSigned(-1);

    private static B2ShapeDef ShapeDef(in PhysicsMaterial material, PhysicsCategory category, PhysicsCategory mask)
    {
        var def = b2DefaultShapeDef();
        def.density = (float)material.Density;
        def.material.friction = (float)material.Friction;
        def.material.restitution = (float)material.Restitution;
        def.filter = new B2Filter((ulong)category, (ulong)mask, 0);
        def.enableHitEvents = true;
        def.enableContactEvents = true;
        return def;
    }

    private static B2BodyType TypeOf(PhysicsBodyKind kind) => kind switch
    {
        PhysicsBodyKind.Kinematic => B2BodyType.b2_kinematicBody,
        PhysicsBodyKind.Dynamic => B2BodyType.b2_dynamicBody,
        _ => B2BodyType.b2_staticBody,
    };

    private static B2Rot RotationOf(double angle) =>
        angle == 0 ? new B2Rot(1, 0) : new B2Rot((float)Math.Cos(angle), (float)Math.Sin(angle));
}
