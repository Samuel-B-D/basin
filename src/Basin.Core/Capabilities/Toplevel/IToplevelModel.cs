using System.Buffers;

namespace Basin.Capabilities;

public interface IToplevelModel
{
    int Enumerate(Span<ToplevelInfo> toplevels);

    bool TryGet(ulong toplevelId, out ToplevelInfo info);

    void AddObserver(IToplevelObserver observer);

    void RemoveObserver(IToplevelObserver observer);

    bool Request(ulong toplevelId, in ToplevelRequest request);

    bool TryFind(Surface surface, out ToplevelInfo info)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var length = 64;
        while (true)
        {
            var buffer = ArrayPool<ToplevelInfo>.Shared.Rent(length);
            var count = Enumerate(buffer);
            if (count < 0)
            {
                length = buffer.Length * 2;
                ArrayPool<ToplevelInfo>.Shared.Return(buffer, clearArray: true);
                continue;
            }

            info = default;
            var found = false;
            for (var i = 0; i < count; i++)
            {
                if (ReferenceEquals(buffer[i].Surface, surface))
                {
                    info = buffer[i];
                    found = true;
                    break;
                }
            }

            ArrayPool<ToplevelInfo>.Shared.Return(buffer, clearArray: true);
            return found;
        }
    }

    bool ReportsCommits(ulong toplevelId) => false;

    void AddCommitObserver(IToplevelCommitObserver observer)
    {
    }

    void RemoveCommitObserver(IToplevelCommitObserver observer)
    {
    }
}
