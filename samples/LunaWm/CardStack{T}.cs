namespace LunaWm;

internal sealed class CardStack<T> where T : class, ILayoutCard
{
    private readonly List<T> _cards = [];

    public IReadOnlyList<T> Cards => _cards;

    public int Count => _cards.Count;

    public List<T>.Enumerator GetEnumerator() => _cards.GetEnumerator();

    public T? Active { get; set; }

    public double Position { get; set; }

    public double X { get; set; }

    public double Left { get; private set; }

    public double Right { get; private set; }

    public bool Contains(T card) => _cards.Contains(card);

    public int IndexOf(T card) => _cards.IndexOf(card);

    public void AddToFront(T card)
    {
        _cards.Remove(card);
        _cards.Add(card);
        Active = card;
        Position = _cards.Count;
        Clamp();
    }

    public void Insert(int index, T card)
    {
        _cards.Remove(card);
        _cards.Insert(Math.Clamp(index, 0, _cards.Count), card);
        Active = card;
        Position = _cards.IndexOf(card);
        Clamp();
    }

    public bool Remove(T card)
    {
        var index = _cards.IndexOf(card);
        if (index < 0)
        {
            return false;
        }

        _cards.RemoveAt(index);
        if (ReferenceEquals(Active, card))
        {
            Active = _cards.Count == 0 ? null : _cards[Math.Min(index, _cards.Count - 1)];
        }

        Clamp();
        return true;
    }

    public void Clamp() => Position = ClampPosition(Position);

    public double ClampPosition(double position) => _cards.Count switch
    {
        <= 1 => 0,
        2 => 0.5,
        <= CardMetrics.MaxStationaryCards => 1,
        _ => Math.Clamp(position, 1, _cards.Count - CardMetrics.MaxStationaryCards + 1),
    };

    public void LayoutOpen(in CardMetrics metrics, double position, double brightnessOfOthers)
    {
        var scale = metrics.ActiveScale;
        var width = metrics.ActiveCardWidth;
        var rightLimit = (scale * 50) + width;
        var leftLimit = -(scale * 100);
        var rotation = scale * metrics.RotationFactor;
        for (var i = 0; i < _cards.Count; i++)
        {
            var x = (i - position) / 3.0 * width;
            if (x > rightLimit)
            {
                x = (x + (rightLimit * 4)) / 5;
            }
            else if (x < leftLimit)
            {
                x = (x + (leftLimit * 4)) / 5;
            }

            var brightness = ReferenceEquals(_cards[i], Active) ? 1 : brightnessOfOthers;
            _cards[i].Target = new CardPose(x, x > 0 ? x / 15 : 0, scale, x * CardMetrics.TouchPadWidth / metrics.Width / rotation, brightness);
        }

        Measure(metrics);
    }

    public void LayoutClosed(in CardMetrics metrics, double brightness)
    {
        var offset = 0.0;
        var stop = _cards.Count > CardMetrics.MaxClosedSpacedCards ? _cards.Count - CardMetrics.MaxClosedSpacedCards : 0;
        for (var i = _cards.Count - 1; i >= 0; i--)
        {
            _cards[i].Target = new CardPose(offset, 0, metrics.InactiveScale, 0, brightness);
            if (i >= stop)
            {
                offset -= CardMetrics.ClosedSpacing;
            }
        }

        Measure(metrics);
    }

    private void Measure(in CardMetrics metrics)
    {
        if (_cards.Count == 0)
        {
            Left = Right = 0;
            return;
        }

        var first = _cards[0].Target;
        var last = _cards[^1].Target;
        Left = Math.Abs(first.X - metrics.HalfExtent(first));
        Right = Math.Abs(last.X + metrics.HalfExtent(last));
    }
}
