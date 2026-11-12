using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace BF2WebAdmin.Server.Extensions;

/// <summary>
/// The one thread a server's modules run on. The admin channel's events, chat commands, the modules' background tasks and
/// every <c>await</c> continuation of theirs (a <see cref="Task.Delay(int)"/>, a database call) run here one at a time, in
/// the order they were queued, so module state needs no locks. Async work interleaves at its awaits, as on a UI thread.
/// <para>
/// Code here must not block (no <c>.Result</c>, <c>.Wait()</c> or <c>Thread.Sleep</c>): it would hold up the server's
/// events, and waiting for other work queued here would never end. CPU-heavy work goes to the thread pool with its own
/// <c>Task.Run</c>. After <see cref="Dispose"/> (the modules were reloaded or the server removed) what is still posted runs
/// on the thread pool, so the old modules' pending work still finishes.
/// </para>
/// </summary>
public sealed class ModuleContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly Thread _thread;
    private readonly ILogger _logger;
    private volatile bool _disposed;

    public ModuleContext(string name, ILogger logger)
    {
        _logger = logger;
        _thread = new Thread(Run) { IsBackground = true, Name = name };
        _thread.Start();
    }

    /// <summary>True on the module thread.</summary>
    public bool IsCurrent => Thread.CurrentThread == _thread;

    /// <summary>Work waiting to run (a gauge for a module thread that can't keep up).</summary>
    public int QueueLength => _queue.Count;

    /// <summary>
    /// Runs <paramref name="work"/> on the module thread with the caller's execution context (so a
    /// <c>GameTextCapture</c> follows it), and completes when the task it returns has, with its result.
    /// </summary>
    public Task InvokeAsync(Func<Task> work)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionContext = ExecutionContext.Capture();
        Post(static state =>
        {
            var (work, done, executionContext) = ((Func<Task>, TaskCompletionSource, ExecutionContext?))state!;
            if (executionContext == null)
                Start(work, done);
            else
                ExecutionContext.Run(executionContext, static s =>
                {
                    var (w, d) = ((Func<Task>, TaskCompletionSource))s!;
                    Start(w, d);
                }, (work, done));
        }, (work, done, executionContext));

        return done.Task;
    }

    private static void Start(Func<Task> work, TaskCompletionSource done)
    {
        try
        {
            // Runs here until its first await; its continuations come back through Post
            work().ContinueWith(static (task, state) =>
            {
                var d = (TaskCompletionSource)state!;
                if (task.IsFaulted)
                    d.TrySetException(task.Exception!.InnerExceptions);
                else if (task.IsCanceled)
                    d.TrySetCanceled();
                else
                    d.TrySetResult();
            }, done, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        catch (Exception e)
        {
            done.TrySetException(e);
        }
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        if (!_disposed)
        {
            try
            {
                _queue.Add((d, state));
                return;
            }
            catch (InvalidOperationException)
            {
                // Disposed meanwhile
            }
        }

        ThreadPool.QueueUserWorkItem(static s => s.d(s.state), (d, state), preferLocal: false);
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (IsCurrent)
        {
            d(state);
            return;
        }

        using var done = new ManualResetEventSlim();
        ExceptionDispatchInfo? error = null;
        Post(s =>
        {
            try
            {
                d(s);
            }
            catch (Exception e)
            {
                error = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                done.Set();
            }
        }, state);
        done.Wait();
        error?.Throw();
    }

    public override SynchronizationContext CreateCopy() => this;

    private void Run()
    {
        SetSynchronizationContext(this);
        foreach (var (callback, state) in _queue.GetConsumingEnumerable())
        {
            try
            {
                callback(state);
            }
            catch (Exception e)
            {
                // An async void method's exception lands here instead of ending the process
                _logger.LogError(e, "Module code on {Thread} failed", _thread.Name);
            }
        }
    }

    /// <summary>Lets the thread finish what is queued and end; later work goes to the thread pool.</summary>
    public void Dispose()
    {
        _disposed = true;
        _queue.CompleteAdding();
    }
}
