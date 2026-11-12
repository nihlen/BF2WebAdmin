namespace BF2WebAdmin.Server.Extensions;

/// <summary>
/// Runs work in the background, one item after the other in the order it was queued, so slow I/O (a database write with
/// retries) never holds up the caller (the admin channel's apply loop) and writes that depend on each other stay in order.
/// A failed item is logged and the next one runs.
/// </summary>
public sealed class SerialTaskQueue(ILogger logger)
{
    private readonly object _lock = new();
    private Task _tail = Task.CompletedTask;

    /// <summary>Queues the work; the returned task completes when it has run (it never fails).</summary>
    public Task Enqueue(string description, Func<Task> work)
    {
        lock (_lock)
        {
            _tail = _tail.ContinueWith(async _ =>
            {
                try
                {
                    await work();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed: {Description}", description);
                }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();

            return _tail;
        }
    }
}
