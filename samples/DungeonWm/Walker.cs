using System.Numerics;
using Basin.Kinematics;
using Basin.Scene3D;

namespace DungeonWm;

internal sealed class Walker
{
    private const float Radius = 0.3f;
    private const float StepHeight = 0.35f;
    private const float JumpSpeed = 4.2f;

    private SpringHandoff _flightPosition = new(60, 1) { Epsilon = 0.01 };
    private SpringHandoff _flightAngles = new(60, 1) { Epsilon = 0.002 };
    private float _flightY;
    private float _verticalSpeed;
    private bool _jump;
    private bool _wasFlying;

    public Walker(WorldCamera camera, DungeonWmSettings settings)
    {
        Camera = camera;
        Settings = settings;
    }

    public WorldCamera Camera { get; }

    public Basin.Physics.PhysicsCharacter3D? Character { get; set; }

    public bool JustJumped { get; private set; }

    public bool JustLanded { get; private set; }

    private bool _supported = true;

    public DungeonWmSettings Settings { get; }

    public bool Forward { get; set; }

    public bool Back { get; set; }

    public bool Left { get; set; }

    public bool Right { get; set; }

    public bool Run { get; set; }

    public bool Grounded { get; private set; } = true;

    public bool Flying => _flightPosition.IsMoving || _flightAngles.IsMoving;

    public bool Moving => Forward || Back || Left || Right || !Grounded || Flying;

    public Vector3 Velocity { get; private set; }

    public float Feet => Camera.Position.Y - Settings.EyeHeight;

    public void Jump()
    {
        if (Grounded)
        {
            _jump = true;
        }
    }

    public void Look(float yawDelta, float pitchDelta)
    {
        if (Flying)
        {
            return;
        }

        Camera.Yaw += yawDelta;
        Camera.Pitch += pitchDelta;
    }

    public void FlyTo(Vector3 position, float yaw, float pitch = 0)
    {
        var from = Camera.Position;
        _flightPosition.Start(from.X, from.Z, 0, 0, position.X, position.Z);
        var targetYaw = Camera.Yaw + WrapAngle(yaw - Camera.Yaw);
        _flightAngles.Start(Camera.Yaw, Camera.Pitch, 0, 0, targetYaw, pitch);
        _flightY = position.Y;
    }

    public void Teleport(Vector3 position, float yaw, float pitch = 0)
    {
        _flightPosition.Stop();
        _flightAngles.Stop();
        Camera.Position = position;
        Character?.Teleport(position - new Vector3(0, Settings.EyeHeight - (DungeonWmPhysics.PlayerHeight * 0.5f), 0));
        Camera.Yaw = yaw;
        Camera.Pitch = pitch;
        _verticalSpeed = 0;
        Grounded = true;
    }

    public void Walk(Vector2 wish, float seconds)
    {
        var flat = Camera.Flat;
        var side = Camera.FlatRight;
        var step = ((flat * wish.Y) + (side * wish.X)) * seconds;
        Camera.Position += step;
    }

    public bool Step(float seconds, World world)
    {
        seconds = Math.Clamp(seconds, 0, 0.1f);
        JustJumped = false;
        JustLanded = false;
        if (!Flying && _wasFlying)
        {
            _wasFlying = false;
            Character?.Teleport(Camera.Position - new Vector3(0, Settings.EyeHeight - (DungeonWmPhysics.PlayerHeight * 0.5f), 0));
        }

        if (Flying)
        {
            _wasFlying = true;
            var nanos = (long)(seconds * 1e9);
            _flightPosition.Advance(nanos);
            _flightAngles.Advance(nanos);
            var y = Camera.Position.Y + ((_flightY - Camera.Position.Y) * Math.Min(1, seconds * 8));
            Camera.Position = new Vector3((float)_flightPosition.X, y, (float)_flightPosition.Y);
            Camera.Yaw = (float)_flightAngles.X;
            Camera.Pitch = (float)_flightAngles.Y;
            Velocity = Vector3.Zero;
            return true;
        }

        var wish = Vector2.Zero;
        if (Forward)
        {
            wish.Y += 1;
        }

        if (Back)
        {
            wish.Y -= 1;
        }

        if (Right)
        {
            wish.X += 1;
        }

        if (Left)
        {
            wish.X -= 1;
        }

        if (Character is { } character)
        {
            var speed = Run ? Settings.RunSpeed : Settings.WalkSpeed;
            var goal = wish == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(wish) * speed;
            character.Move(goal, Camera.Flat);
            if (_jump)
            {
                character.Jump();
                _jump = false;
                JustJumped = true;
            }

            return wish != Vector2.Zero || !character.Supported || character.Body.IsAwake;
        }

        var moved = false;
        if (wish != Vector2.Zero)
        {
            wish = Vector2.Normalize(wish) * (Run ? Settings.RunSpeed : Settings.WalkSpeed);
            var before = Camera.Position;
            Walk(wish, seconds);
            Collide(world);
            Velocity = (Camera.Position - before) / MathF.Max(seconds, 1e-4f);
            moved = true;
        }
        else
        {
            Velocity = Velocity with { X = 0, Z = 0 };
        }

        if (_jump)
        {
            _verticalSpeed = JumpSpeed;
            Grounded = false;
            _jump = false;
        }

        if (!Grounded || _verticalSpeed != 0)
        {
            _verticalSpeed -= Settings.Gravity * seconds;
            Camera.Position += new Vector3(0, _verticalSpeed * seconds, 0);
            moved = true;
        }

        var ground = GroundUnder(world);
        if (Feet <= ground)
        {
            Camera.Position = Camera.Position with { Y = ground + Settings.EyeHeight };
            _verticalSpeed = 0;
            Grounded = true;
        }
        else
        {
            Grounded = false;
        }

        var ceiling = CeilingOver(world);
        if (Camera.Position.Y + 0.1f > ceiling)
        {
            Camera.Position = Camera.Position with { Y = ceiling - 0.1f };
            _verticalSpeed = MathF.Min(_verticalSpeed, 0);
        }

        return moved;
    }

    public void WalkBy(Vector2 meters, World world)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(meters.X), MathF.Abs(meters.Y)) / 0.1f));
        for (var i = 0; i < steps; i++)
        {
            Walk(meters / steps, 1);
            Collide(world);
        }

        Character?.Teleport(Camera.Position - new Vector3(0, Settings.EyeHeight - (DungeonWmPhysics.PlayerHeight * 0.5f), 0));
    }

    public void Sync()
    {
        if (Flying || Character is not { } character)
        {
            return;
        }

        var center = character.Body.Pose.Position;
        var eye = center + new Vector3(0, Settings.EyeHeight - (DungeonWmPhysics.PlayerHeight * 0.5f), 0);
        var supported = character.Supported;
        if (supported)
        {
            eye.Y = MathF.Round(eye.Y * 1000) / 1000;
        }

        Camera.Position = eye;
        Velocity = character.Velocity;
        JustLanded = supported && !_supported;
        _supported = supported;
        Grounded = supported;
    }

    private void Collide(World world)
    {
        var position = Camera.Position;
        var feet = position.Y - Settings.EyeHeight + StepHeight;
        var head = position.Y + 0.1f;
        for (var pass = 0; pass < 3; pass++)
        {
            foreach (var room in world.Rooms)
            {
                if (!Near(room.Bounds, position))
                {
                    continue;
                }

                foreach (var box in room.Blockers)
                {
                    if (box.Max.Y < feet || box.Min.Y > head)
                    {
                        continue;
                    }

                    var closest = new Vector2(
                        Math.Clamp(position.X, box.Min.X, box.Max.X),
                        Math.Clamp(position.Z, box.Min.Z, box.Max.Z));
                    var offset = new Vector2(position.X, position.Z) - closest;
                    var distance = offset.Length();
                    if (distance >= Radius)
                    {
                        continue;
                    }

                    var push = distance > 1e-5f
                        ? offset / distance * (Radius - distance)
                        : OutOf(box, position);
                    position += new Vector3(push.X, 0, push.Y);
                }
            }
        }

        Camera.Position = position;
    }

    private static Vector2 OutOf(in WorldBounds box, Vector3 position)
    {
        var left = position.X - box.Min.X;
        var right = box.Max.X - position.X;
        var front = position.Z - box.Min.Z;
        var back = box.Max.Z - position.Z;
        var least = MathF.Min(MathF.Min(left, right), MathF.Min(front, back));
        return least == left ? new Vector2(-(left + Radius), 0)
            : least == right ? new Vector2(right + Radius, 0)
            : least == front ? new Vector2(0, -(front + Radius))
            : new Vector2(0, back + Radius);
    }

    private float GroundUnder(World world)
    {
        var position = Camera.Position;
        var feet = position.Y - Settings.EyeHeight;
        var ground = 0f;
        foreach (var room in world.Rooms)
        {
            if (!Near(room.Bounds, position))
            {
                continue;
            }

            foreach (var box in room.Blockers)
            {
                if (position.X >= box.Min.X && position.X <= box.Max.X &&
                    position.Z >= box.Min.Z && position.Z <= box.Max.Z &&
                    box.Max.Y <= feet + StepHeight && box.Max.Y > ground)
                {
                    ground = box.Max.Y;
                }
            }
        }

        return ground;
    }

    private float CeilingOver(World world)
    {
        var position = Camera.Position;
        var ceiling = float.MaxValue;
        foreach (var room in world.Rooms)
        {
            if (!Near(room.Bounds, position))
            {
                continue;
            }

            foreach (var box in room.Blockers)
            {
                if (position.X >= box.Min.X && position.X <= box.Max.X &&
                    position.Z >= box.Min.Z && position.Z <= box.Max.Z &&
                    box.Min.Y > position.Y && box.Min.Y < ceiling)
                {
                    ceiling = box.Min.Y;
                }
            }
        }

        return ceiling;
    }

    private static bool Near(in WorldBounds bounds, Vector3 point) =>
        point.X > bounds.Min.X - 2 && point.X < bounds.Max.X + 2 &&
        point.Z > bounds.Min.Z - 2 && point.Z < bounds.Max.Z + 2;

    private static float WrapAngle(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= 2 * MathF.PI;
        }

        while (angle < -MathF.PI)
        {
            angle += 2 * MathF.PI;
        }

        return angle;
    }
}
