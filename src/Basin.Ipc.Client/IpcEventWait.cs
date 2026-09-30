namespace Basin.Ipc;

public static class IpcEventWait
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public const int DefaultMaxCollected = 64;

    public static async Task<IpcEventWaitResult> RunAsync(
        string socketPath,
        IReadOnlyList<string> events,
        Func<IpcEvent, bool>? match = null,
        Func<CancellationToken, Task<bool>>? then = null,
        TimeSpan? timeout = null,
        TimeSpan collect = default,
        int maxCollected = DefaultMaxCollected,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(socketPath);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCollected, 1);
        await using var waiter = await BasinIpcClient.ConnectAsync(socketPath, cancellationToken).ConfigureAwait(false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? DefaultTimeout);
        var subscribed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = waiter.SubscribeAsync(events, subscribed.SetResult, deadline.Token).GetAsyncEnumerator(deadline.Token);
        var matches = new List<IpcEvent>();
        Task<bool>? pending = null;
        try
        {
            var first = stream.MoveNextAsync().AsTask();
            pending = first;
            var ready = await Task.WhenAny(subscribed.Task, first).ConfigureAwait(false);
            if (ready == first && !subscribed.Task.IsCompleted)
            {
                _ = await first.ConfigureAwait(false);
            }

            if (then is not null && !await then(cancellationToken).ConfigureAwait(false))
            {
                return new IpcEventWaitResult(matches, Stopped: true);
            }

            var next = first;
            var collecting = false;
            while (true)
            {
                bool more;
                try
                {
                    more = await next.ConfigureAwait(false);
                    pending = null;
                }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    pending = null;
                    break;
                }

                if (!more)
                {
                    break;
                }

                var item = stream.Current;
                if (match is null || match(item))
                {
                    matches.Add(item);
                    if (collect <= TimeSpan.Zero || matches.Count >= maxCollected)
                    {
                        break;
                    }

                    if (!collecting)
                    {
                        collecting = true;
                        deadline.CancelAfter(collect);
                    }
                }

                next = stream.MoveNextAsync().AsTask();
                pending = next;
            }
        }
        finally
        {
            await deadline.CancelAsync().ConfigureAwait(false);
            if (pending is not null)
            {
                try
                {
                    _ = await pending.ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is OperationCanceledException or IpcCallException or IOException or ObjectDisposedException or System.Net.Sockets.SocketException)
                {
                }
            }

            await stream.DisposeAsync().ConfigureAwait(false);
        }

        return new IpcEventWaitResult(matches, Stopped: false);
    }
}
