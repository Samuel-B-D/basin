using Basin;
using Basin.Scene;

namespace System7Wm;

internal sealed class ZoomRects : IDisposable
{
    private const int Steps = 12;
    private const int Trail = 4;
    private const int IntervalMs = 12;

    private readonly GrayOutline[] _outlines;
    private readonly IEventSource _timer;
    private readonly Action _repaint;
    private (int X, int Y, double Scale, int Width, int Height) _output;
    private Box _from;
    private Box _to;
    private int _step;
    private Action? _done;

    public ZoomRects(SceneTree layer, ICompositorEventLoop loop, Action repaint)
    {
        _outlines = [.. Enumerable.Range(0, Trail).Select(_ => new GrayOutline(layer))];
        _repaint = repaint;
        _timer = loop.AddTimer(Tick);
    }

    public bool Running => _done is not null;

    public void Start(int originX, int originY, double scale, int outputWidth, int outputHeight, Box from, Box to, Action done)
    {
        Finish();
        _output = (originX, originY, scale, outputWidth, outputHeight);
        _from = from;
        _to = to;
        _step = 0;
        _done = done;
        Tick();
    }

    public void Finish()
    {
        if (_done is not { } done)
        {
            return;
        }

        _done = null;
        _timer.UpdateTimer(0);
        foreach (var outline in _outlines)
        {
            outline.Hide();
        }

        _repaint();
        done();
    }

    public void Dispose()
    {
        _done = null;
        _timer.Remove();
        foreach (var outline in _outlines)
        {
            outline.Dispose();
        }
    }

    private void Tick()
    {
        if (_done is null)
        {
            return;
        }

        _step++;
        if (_step > Steps + Trail)
        {
            Finish();
            return;
        }

        for (var i = 0; i < Trail; i++)
        {
            var step = _step - i;
            if (step < 1 || step > Steps)
            {
                _outlines[i].Hide();
                continue;
            }

            var t = step / (double)Steps;
            t *= t;
            var box = new Box(
                Lerp(_from.X, _to.X, t),
                Lerp(_from.Y, _to.Y, t),
                Math.Max(1, Lerp(_from.Width, _to.Width, t)),
                Math.Max(1, Lerp(_from.Height, _to.Height, t)));
            _outlines[i].Show(_output.X, _output.Y, _output.Scale, _output.Width, _output.Height, box, 0);
        }

        _repaint();
        _timer.UpdateTimer(IntervalMs);
    }

    private static int Lerp(int from, int to, double t) => (int)Math.Round(from + ((to - from) * t));
}
