using System.Globalization;
using System.Text.Json;
using Xunit;

namespace TinyComp.Tests;

internal static class PhysicsRows
{
    internal const string Physics = "[physics]\nenable = true\n";

    public static TheoryData<string> Engines => new() { "bepu", "box2d" };

    internal static string PhysicsOn(string engine) => $"{Physics}engine = \"{engine}\"\n";

    internal const string Canvas = "[canvas]\nenable = true\ngrid = \"never\"\nanimation_ms = 100\n";

    internal static Dictionary<string, string> Body(CanvasSession session)
    {
        session.Send("where");
        var line = session.WaitForLine("PHYS ");
        Assert.NotNull(line);
        return Fields(line!);
    }

    internal static Dictionary<string, string> Fields(string line)
    {
        var fields = new Dictionary<string, string>();
        foreach (var part in line.Split(' '))
        {
            var equals = part.IndexOf('=');
            if (equals > 0)
            {
                fields[part[..equals]] = part[(equals + 1)..];
            }
        }

        return fields;
    }

    internal static Dictionary<string, string> Rest(CanvasSession session, int timeoutMillis = 10000)
    {
        var deadline = Environment.TickCount64 + timeoutMillis;
        while (true)
        {
            var body = Body(session);
            if (body.GetValueOrDefault("awake") == "no" || Environment.TickCount64 > deadline)
            {
                return body;
            }

            Thread.Sleep(150);
        }
    }

    internal static double Number(Dictionary<string, string> fields, string key) =>
        double.Parse(fields[key], CultureInfo.InvariantCulture);

    internal static ulong[] WindowIds(CanvasSession session)
    {
        session.Send("ipc windows/list {}");
        var line = session.WaitForLine("OK windows/list ");
        Assert.NotNull(line);
        var json = line!["OK windows/list windows=".Length..];
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(window => window.GetProperty("id").GetUInt64())];
    }

    internal static List<string> TouchTwist(double centerX, double centerY, double radius, double degrees, int steps = 24, int fingers = 2)
    {
        var lines = new List<string>();
        for (var i = 0; i <= steps; i++)
        {
            var angle = degrees * Math.PI / 180 * i / steps;
            for (var finger = 0; finger < fingers; finger++)
            {
                var x = centerX + (radius * Math.Cos(angle + (finger * Math.Tau / fingers)));
                var y = centerY + (radius * Math.Sin(angle + (finger * Math.Tau / fingers)));
                lines.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"ipc input/touch {{\"kind\":\"{(i == 0 ? "down" : "motion")}\",\"id\":{finger + 1},\"x\":{x:F2},\"y\":{y:F2}}}"));
            }

            lines.Add("ipc input/touch {\"kind\":\"frame\"}");
        }

        for (var finger = 0; finger < fingers; finger++)
        {
            lines.Add($"ipc input/touch {{\"kind\":\"up\",\"id\":{finger + 1}}}");
        }

        lines.Add("ipc input/touch {\"kind\":\"frame\"}");
        return lines;
    }

    internal static (double X, double Y) Titlebar(CanvasSession session)
    {
        session.Send("ipc windows/list {}");
        var line = session.WaitForLine("OK windows/list ");
        Assert.NotNull(line);
        using var document = JsonDocument.Parse(line!["OK windows/list windows=".Length..]);
        var window = document.RootElement[0];
        var frame = window.GetProperty("geometry");
        var client = window.GetProperty("client_geometry");
        var top = frame.GetProperty("y").GetDouble();
        return (
            frame.GetProperty("x").GetDouble() + (frame.GetProperty("width").GetDouble() / 2),
            top + ((client.GetProperty("y").GetDouble() - top) / 2));
    }

    internal static Dictionary<string, string>? Tilt(CanvasSession session)
    {
        session.Send("where");
        _ = session.WaitForLine("WIN ");
        var line = session.WaitForLine("TILT ", 1000);
        return line is null ? null : Fields(line);
    }
}
