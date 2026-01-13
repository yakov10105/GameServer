namespace GameServer.Infrastructure.Concurrency;

public sealed class LocalSemaphoreProvider(ILogger<LocalSemaphoreProvider> logger) : ISynchronizationProvider
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireLockAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        logger.LockAcquisitionStarted(resourceId);
        var semaphore = _locks.GetOrAdd(resourceId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        logger.LockAcquired(resourceId);
        return new SemaphoreLock(semaphore);
    }

    /// <summary>
    /// Acquires locks for both resources in a deterministic order to prevent deadlocks.
    /// Always locks the resource with the smaller GUID first.
    /// </summary>
    public async Task<IDisposable> AcquireLocksAsync(Guid resourceId1, Guid resourceId2, CancellationToken cancellationToken = default)
    {
        var (firstId, secondId) = resourceId1.CompareTo(resourceId2) < 0
            ? (resourceId1, resourceId2)
            : (resourceId2, resourceId1);

        logger.DualLockAcquisitionStarted(firstId, secondId);

        var firstSemaphore = _locks.GetOrAdd(firstId, _ => new SemaphoreSlim(1, 1));
        var secondSemaphore = _locks.GetOrAdd(secondId, _ => new SemaphoreSlim(1, 1));

        await firstSemaphore.WaitAsync(cancellationToken);
        logger.LockAcquired(firstId);

        try
        {
            await secondSemaphore.WaitAsync(cancellationToken);
            logger.LockAcquired(secondId);
        }
        catch
        {
            logger.DualLockRollback(firstId);
            firstSemaphore.Release();
            throw;
        }

        return new CompositeSemaphoreLock(firstSemaphore, secondSemaphore);
    }

    private sealed class SemaphoreLock(SemaphoreSlim semaphore) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }

    private sealed class CompositeSemaphoreLock(
        SemaphoreSlim first,
        SemaphoreSlim second) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                second.Release();
                first.Release();
            }
        }
    }
}

