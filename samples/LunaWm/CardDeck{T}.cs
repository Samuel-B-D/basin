namespace LunaWm;

internal sealed class CardDeck<T> where T : class, ILayoutCard
{
    private readonly List<CardStack<T>> _stacks = [];

    public IReadOnlyList<CardStack<T>> Stacks => _stacks;

    public CardStack<T>? ActiveStack { get; private set; }

    public List<CardStack<T>>.Enumerator GetEnumerator() => _stacks.GetEnumerator();

    public T? ActiveCard => ActiveStack?.Active;

    public int Count
    {
        get
        {
            var count = 0;
            foreach (var stack in _stacks)
            {
                count += stack.Count;
            }

            return count;
        }
    }

    public bool IsEmpty => _stacks.Count == 0;

    public int ActiveIndex => ActiveStack is null ? -1 : _stacks.IndexOf(ActiveStack);

    public CardStack<T>? StackOf(T card)
    {
        foreach (var stack in _stacks)
        {
            if (stack.Contains(card))
            {
                return stack;
            }
        }

        return null;
    }

    public bool Contains(T card) => StackOf(card) is not null;

    public CardStack<T> Add(T card, T? sibling)
    {
        if (sibling is not null && StackOf(sibling) is { } joined)
        {
            joined.AddToFront(card);
            ActiveStack = joined;
            return joined;
        }

        var stack = new CardStack<T>();
        stack.AddToFront(card);
        var at = ActiveStack is null ? _stacks.Count : _stacks.IndexOf(ActiveStack) + 1;
        _stacks.Insert(at, stack);
        ActiveStack = stack;
        return stack;
    }

    public CardStack<T> Append(T card)
    {
        var stack = new CardStack<T>();
        stack.AddToFront(card);
        _stacks.Add(stack);
        ActiveStack ??= stack;
        return stack;
    }

    public bool Remove(T card)
    {
        if (StackOf(card) is not { } stack)
        {
            return false;
        }

        stack.Remove(card);
        if (stack.Count > 0)
        {
            return true;
        }

        var index = _stacks.IndexOf(stack);
        _stacks.RemoveAt(index);
        if (ReferenceEquals(ActiveStack, stack))
        {
            ActiveStack = _stacks.Count == 0 ? null : _stacks[Math.Min(index, _stacks.Count - 1)];
        }

        return true;
    }

    public void Activate(T card)
    {
        if (StackOf(card) is { } stack)
        {
            ActiveStack = stack;
            if (!ReferenceEquals(stack.Active, card))
            {
                stack.Active = card;
                stack.Position = stack.IndexOf(card);
                stack.Clamp();
            }
        }
    }

    public void ActivateStack(int index)
    {
        if (index >= 0 && index < _stacks.Count)
        {
            ActiveStack = _stacks[index];
        }
    }

    public void MoveToStack(T card, int stackIndex, int position)
    {
        if (stackIndex < 0 || stackIndex >= _stacks.Count)
        {
            return;
        }

        var target = _stacks[stackIndex];
        var source = StackOf(card);
        if (ReferenceEquals(source, target))
        {
            target.Insert(position, card);
            ActiveStack = target;
            return;
        }

        source?.Remove(card);
        target.Insert(position, card);
        ActiveStack = target;
        if (source is { Count: 0 })
        {
            _stacks.Remove(source);
        }
    }

    public void MoveToNewStack(T card, int insertAt)
    {
        var source = StackOf(card);
        if (source is { Count: 1 })
        {
            var from = _stacks.IndexOf(source);
            _stacks.RemoveAt(from);
            _stacks.Insert(Math.Clamp(insertAt > from ? insertAt - 1 : insertAt, 0, _stacks.Count), source);
            ActiveStack = source;
            return;
        }

        source?.Remove(card);
        var stack = new CardStack<T>();
        stack.AddToFront(card);
        _stacks.Insert(Math.Clamp(insertAt, 0, _stacks.Count), stack);
        ActiveStack = stack;
    }

    public void MoveStack(int from, int to)
    {
        if (from < 0 || from >= _stacks.Count)
        {
            return;
        }

        var stack = _stacks[from];
        _stacks.RemoveAt(from);
        _stacks.Insert(Math.Clamp(to, 0, _stacks.Count), stack);
        ActiveStack = stack;
    }

    public T? Neighbor(T card, int direction)
    {
        T? previous = null;
        var found = false;
        foreach (var stack in _stacks)
        {
            foreach (var each in stack)
            {
                if (found)
                {
                    return each;
                }

                if (ReferenceEquals(each, card))
                {
                    if (direction < 0)
                    {
                        return previous;
                    }

                    found = true;
                }

                previous = each;
            }
        }

        return null;
    }

    public void LayoutCardView(in CardMetrics metrics, double scroll)
    {
        if (ActiveStack is not { } active)
        {
            return;
        }

        active.LayoutOpen(metrics, active.Position, metrics.Dim);
        active.X = (metrics.Width / 2.0) + scroll;
        var index = _stacks.IndexOf(active);
        var center = active.X - active.Left - metrics.Gap;
        for (var i = index - 1; i >= 0; i--)
        {
            var stack = _stacks[i];
            stack.LayoutClosed(metrics, metrics.Dim);
            center -= stack.Right;
            stack.X = center;
            center -= metrics.Gap + stack.Left;
        }

        center = active.X + active.Right + metrics.Gap;
        for (var i = index + 1; i < _stacks.Count; i++)
        {
            var stack = _stacks[i];
            stack.LayoutClosed(metrics, metrics.Dim);
            center += stack.Left;
            stack.X = center;
            center += metrics.Gap + stack.Right;
        }

        foreach (var stack in _stacks)
        {
            foreach (var card in stack)
            {
                var local = card.Target;
                card.Target = local with { X = stack.X + local.X, Y = metrics.Origin + local.Y };
            }
        }
    }

    public void LayoutMaximized(in CardMetrics metrics, bool fullscreen)
    {
        if (ActiveStack is not { Active: { } focus } active)
        {
            return;
        }

        LayoutCardView(metrics, 0);
        var activeIndex = _stacks.IndexOf(active);
        var focusIndex = active.IndexOf(focus);
        for (var s = 0; s < _stacks.Count; s++)
        {
            var stack = _stacks[s];
            for (var c = 0; c < stack.Count; c++)
            {
                var card = stack.Cards[c];
                if (ReferenceEquals(card, focus))
                {
                    card.Target = fullscreen ? metrics.Fullscreen : metrics.Maximized;
                    continue;
                }

                var left = s < activeIndex || (s == activeIndex && c < focusIndex);
                var pose = card.Target;
                card.Target = pose with { X = pose.X + (left ? -metrics.Width : metrics.Width) };
            }
        }
    }

    public int StackIndexAt(in CardMetrics metrics, double x)
    {
        for (var i = 0; i < _stacks.Count; i++)
        {
            var stack = _stacks[i];
            if (x >= stack.X - stack.Left - (metrics.Gap / 2.0) && x < stack.X + stack.Right + (metrics.Gap / 2.0))
            {
                return i;
            }
        }

        return -1;
    }
}
