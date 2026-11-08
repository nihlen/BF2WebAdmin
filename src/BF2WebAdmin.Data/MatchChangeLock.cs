using System;
using System.Threading;
using System.Threading.Tasks;

namespace BF2WebAdmin.Data;

/// <summary>
/// Serializes every write that gives a match a new <see cref="Entities.Match.ChangeSeq"/>, from taking the number to the
/// commit. A reader of the export (everything above cursor N, in order) could otherwise see N+1 committed before N and
/// skip N for good. One lock per process: all repositories of every module manager share it, and WebAdmin is one process.
/// </summary>
public static class MatchChangeLock
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public static async Task<IDisposable> AcquireAsync()
    {
        await Semaphore.WaitAsync();
        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                Semaphore.Release();
        }
    }
}
