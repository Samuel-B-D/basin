using Basin.Config;
using Basin.Physics;

namespace TinyComp;

internal sealed class PhysicsSetting
{
    public const double DefaultGravity = 0;

    public static readonly double[] DefaultGravitySteps = [0, 2000];

    public static readonly string[] ImpactNames = ["squash", "shake"];

    public static readonly string[] EngineNames = ["bepu", "box2d"];

    public static PhysicsSetting Defaults { get; } = new();

    public bool Enable { get; init; }

    public string Engine { get; init; } = "bepu";

    public PhysicsEngine EngineInstance => Engine == "box2d" ? Box2DEngine.Instance : BepuEngine.Instance;

    public double Gravity { get; init; } = DefaultGravity;

    public double[] GravitySteps { get; init; } = DefaultGravitySteps;

    public double Friction { get; init; } = 0.3;

    public double Restitution { get; init; } = 0.2;

    public double Density { get; init; } = 1.0;

    public double Damping { get; init; } = 2.0;

    public double ThrowMultiplier { get; init; } = 1.0;

    public double MaxThrowSpeed { get; init; } = 6000;

    public double StopSpeed { get; init; } = 20;

    public double ImpactSpeed { get; init; } = 600;

    public bool Rotation { get; init; } = true;

    public bool Upright { get; init; } = true;

    public IReadOnlyList<string> Impacts { get; init; } = ["squash"];

    public IReadOnlyDictionary<int, double> WorkspaceGravity { get; init; } = new Dictionary<int, double>();

    public bool Squash => Impacts.Contains("squash");

    public bool Shake => Impacts.Contains("shake");

    public double GravityFor(int workspaceNumber) =>
        WorkspaceGravity.TryGetValue(workspaceNumber, out var gravity) ? gravity : Gravity;

    public static PhysicsSetting Parse(TomlReader physics)
    {
        ArgumentNullException.ThrowIfNull(physics);
        var log = physics.Log;
        var fallback = Defaults;
        var present = physics.Table.ContainsKey("impacts");
        var impacts = physics.Words("impacts") ?? (present ? [] : fallback.Impacts.ToArray());
        foreach (var name in impacts)
        {
            if (Array.IndexOf(ImpactNames, name) < 0)
            {
                log.Warn($"{physics.Where("impacts")}: \"{name}\" is not squash or shake, ignored");
            }
        }

        var engine = physics.Choice("engine", fallback.Engine, EngineNames);
        var steps = physics.Numbers("gravity_steps") ?? fallback.GravitySteps;
        var workspaces = new Dictionary<int, double>();
        if (physics.Section("workspace") is { } table)
        {
            foreach (var key in table.Table.Keys.ToArray())
            {
                if (!int.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) ||
                    number < 1)
                {
                    log.Warn($"{table.Where(key)}: a workspace is a number from 1, ignored");
                    _ = table.Raw(key);
                    continue;
                }

                if (table.Section(key) is { } workspace)
                {
                    workspaces[number] = workspace.Number("gravity", fallback.Gravity);
                }
            }
        }

        return new PhysicsSetting
        {
            Enable = physics.Flag("enable", fallback.Enable),
            Engine = engine,
            Gravity = physics.Number("gravity", fallback.Gravity),
            GravitySteps = steps.Length > 0 ? steps : fallback.GravitySteps,
            Friction = Math.Clamp(physics.Number("friction", fallback.Friction), 0, 2),
            Restitution = Math.Clamp(physics.Number("restitution", fallback.Restitution), 0, 1),
            Density = Math.Clamp(physics.Number("density", fallback.Density), 0.01, 100),
            Damping = Math.Clamp(physics.Number("damping", fallback.Damping), 0, 50),
            ThrowMultiplier = Math.Clamp(physics.Number("throw_multiplier", fallback.ThrowMultiplier), 0, 10),
            MaxThrowSpeed = Math.Clamp(physics.Number("max_throw_speed", fallback.MaxThrowSpeed), 0, 100_000),
            StopSpeed = Math.Clamp(physics.Number("stop_speed", fallback.StopSpeed), 0, 1000),
            ImpactSpeed = Math.Clamp(physics.Number("impact_speed", fallback.ImpactSpeed), 0, 100_000),
            Rotation = physics.Flag("rotation", fallback.Rotation),
            Upright = physics.Flag("upright", fallback.Upright),
            Impacts = impacts.Where(name => Array.IndexOf(ImpactNames, name) >= 0).Distinct().ToArray(),
            WorkspaceGravity = workspaces,
        };
    }
}
