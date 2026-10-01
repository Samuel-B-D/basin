using System.Numerics;
using Basin.Scene3D;
using Basin.Scene3D.Gltf;

namespace DungeonWm;

internal sealed class DungeonWmAvatar
{
    private const float FadeIn = 0.1f;
    private const float FadeOut = 0.2f;

    private readonly DungeonWmSettings _settings;
    private readonly SkeletonClip? _idle;
    private readonly SkeletonClip? _walk;
    private readonly SkeletonClip? _run;
    private readonly SkeletonClip? _jumpStart;
    private readonly SkeletonClip? _airborne;
    private readonly SkeletonClip? _land;
    private readonly Dictionary<string, SkeletonClip> _oneShots = new(StringComparer.Ordinal);
    private SkeletonClip? _oneShot;
    private float _oneShotTime;
    private float _time;
    private float _airTime;
    private float _landTime = float.MaxValue;
    private bool _airborneState;

    public DungeonWmAvatar(GlbAvatarModel model, DungeonWmSettings settings, Action<string> report)
    {
        Model = model;
        _settings = settings;
        Avatar = new WorldAvatar(model.Skeleton, model.Mesh, model.Attachments) { Scale = settings.AvatarScale };
        _idle = Clip("idle", report);
        _walk = Clip("walk", report);
        _run = Clip("run", report);
        _jumpStart = Clip("jump-start", report);
        _airborne = Clip("airborne", report);
        _land = Clip("land", report);
        WalkContacts = FootContacts.Find(model.Skeleton, _walk);
        RunContacts = FootContacts.Find(model.Skeleton, _run);
        foreach (var state in new[] { "pick-up", "throw", "interact" })
        {
            if (Clip(state, report) is { } clip)
            {
                _oneShots[state] = clip;
            }
        }

        foreach (var name in settings.AvatarShow)
        {
            if (!model.Attachments.Any(a => a.Name == name))
            {
                report($"[avatar] show names {name}, which the model does not carry");
            }
        }

        Evaluate(0, 0, false, true);
    }

    public GlbAvatarModel Model { get; }

    public WorldAvatar Avatar { get; }

    public string? Playing => _oneShot?.Name;

    public bool Animating { get; private set; } = true;

    public FootContacts? WalkContacts { get; }

    public FootContacts? RunContacts { get; }

    public int StepFoot { get; private set; } = -1;

    public bool StepRunning { get; private set; }

    public bool Striding { get; private set; }

    public void Trigger(string state)
    {
        if (_oneShots.TryGetValue(state, out var clip))
        {
            _oneShot = clip;
            _oneShotTime = 0;
        }
    }

    public void Jumped()
    {
        _airborneState = true;
        _airTime = 0;
    }

    public void Landed()
    {
        if (_airborneState)
        {
            _airborneState = false;
            _landTime = 0;
        }
    }

    public void Update(float seconds, Vector3 feet, float yaw, float speed, bool running, bool supported)
    {
        _time += seconds;
        _airTime += seconds;
        _landTime += seconds;
        if (_oneShot is not null)
        {
            _oneShotTime += seconds;
            if (_oneShotTime >= _oneShot.Duration)
            {
                _oneShot = null;
            }
        }

        if (!supported && !_airborneState && _airTime > 0.2f)
        {
            _airborneState = true;
            _airTime = 0.3f;
        }

        Avatar.Position = feet;
        Avatar.Yaw = yaw;
        Evaluate(speed, _time, running, supported);
        var runWeight = running ? Math.Clamp((speed - _settings.WalkSpeed) / MathF.Max(_settings.RunSpeed - _settings.WalkSpeed, 0.1f), 0, 1) : 0;
        StepRunning = runWeight > 0.5f;
        Striding = supported && !_airborneState && speed > 0.3f;
        var contacts = StepRunning ? RunContacts : WalkContacts;
        StepFoot = Striding && contacts is not null ? contacts.Crossed(_time - seconds, _time) : -1;
    }

    public void Freeze(float time)
    {
        _oneShot = null;
        _airborneState = false;
        _landTime = float.MaxValue;
        Evaluate(0, time, false, true);
    }

    private void Evaluate(float speed, float time, bool running, bool supported)
    {
        var pose = Avatar.Pose;
        pose.Reset();
        if (_idle is { } idle)
        {
            pose.Apply(idle, time, 1, loop: true);
        }

        var walkWeight = Math.Clamp(speed / MathF.Max(_settings.WalkSpeed, 0.1f), 0, 1);
        if (_walk is { } walk && walkWeight > 0.01f)
        {
            pose.Apply(walk, time, walkWeight, loop: true);
        }

        var runWeight = running ? Math.Clamp((speed - _settings.WalkSpeed) / MathF.Max(_settings.RunSpeed - _settings.WalkSpeed, 0.1f), 0, 1) : 0;
        if (_run is { } run && runWeight > 0.01f)
        {
            pose.Apply(run, time, runWeight, loop: true);
        }

        if (_airborneState)
        {
            if (_jumpStart is { } start && _airTime < start.Duration)
            {
                pose.Apply(start, _airTime, 1, loop: false);
            }
            else if (_airborne is { } air)
            {
                pose.Apply(air, _airTime, 1, loop: true);
            }
        }
        else if (_land is { } land && _landTime < land.Duration)
        {
            pose.Apply(land, _landTime, Fade(_landTime, land.Duration), loop: false);
        }

        if (_oneShot is { } shot)
        {
            pose.Apply(shot, _oneShotTime, Fade(_oneShotTime, shot.Duration), loop: false);
        }

        pose.Resolve();
        Animating = speed > 0.1f || !supported || _airborneState || _oneShot is not null ||
            (_land is { } landing && _landTime < landing.Duration) || _idle is not null;
    }

    private static float Fade(float time, float duration) =>
        MathF.Min(Math.Clamp(time / FadeIn, 0, 1), Math.Clamp((duration - time) / FadeOut, 0, 1));

    private SkeletonClip? Clip(string state, Action<string> report)
    {
        if (!_settings.AvatarClips.TryGetValue(state, out var name))
        {
            return null;
        }

        if (Model.Clips.TryGetValue(name, out var clip))
        {
            return clip;
        }

        report($"[avatar.clips] {state} names {name}, which the model does not carry");
        return null;
    }
}
