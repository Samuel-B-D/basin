namespace LunaWm;

internal static class QtEasing
{
    private const double ElasticPeriod = 0.3;
    private const double BackOvershoot = 1.70158;

    public static double Evaluate(QtCurve curve, double t)
    {
        t = Math.Clamp(t, 0, 1);
        var value = (int)curve;
        if (value <= 0 || value > (int)QtCurve.OutInBounce)
        {
            return t;
        }

        var family = (value - 1) / 4;
        return ((value - 1) % 4) switch
        {
            0 => In(family, t),
            1 => Out(family, t),
            2 => t < 0.5 ? In(family, 2 * t) / 2 : 1 - (In(family, 2 - (2 * t)) / 2),
            _ => t < 0.5 ? Out(family, 2 * t) / 2 : (In(family, (2 * t) - 1) / 2) + 0.5,
        };
    }

    private static double In(int family, double t) => family switch
    {
        0 => t * t,
        1 => t * t * t,
        2 => t * t * t * t,
        3 => t * t * t * t * t,
        4 => 1 - Math.Cos(t * Math.PI / 2),
        5 => t is 0 or 1 ? t : Math.Pow(2, 10 * (t - 1)) - 0.001,
        6 => 1 - Math.Sqrt(1 - (t * t)),
        7 => 1 - Out(family, 1 - t),
        8 => t * t * (((BackOvershoot + 1) * t) - BackOvershoot),
        _ => 1 - BounceOut(1 - t),
    };

    private static double Out(int family, double t) => family switch
    {
        5 => t >= 1 ? 1 : 1.001 * (1 - Math.Pow(2, -10 * t)),
        7 => ElasticOut(t),
        9 => BounceOut(t),
        _ => 1 - In(family, 1 - t),
    };

    private static double ElasticOut(double t)
    {
        if (t is 0 or 1)
        {
            return t;
        }

        var s = ElasticPeriod / 4;
        return (Math.Pow(2, -10 * t) * Math.Sin((t - s) * (2 * Math.PI) / ElasticPeriod)) + 1;
    }

    private static double BounceOut(double t)
    {
        if (t >= 1)
        {
            return 1;
        }

        if (t < 4 / 11.0)
        {
            return 7.5625 * t * t;
        }

        if (t < 8 / 11.0)
        {
            t -= 6 / 11.0;
            return (7.5625 * t * t) + 0.75;
        }

        if (t < 10 / 11.0)
        {
            t -= 9 / 11.0;
            return (7.5625 * t * t) + 0.9375;
        }

        t -= 21 / 22.0;
        return (7.5625 * t * t) + 0.984375;
    }
}
