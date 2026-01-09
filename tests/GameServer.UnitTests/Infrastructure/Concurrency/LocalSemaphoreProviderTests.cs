using GameServer.Infrastructure.Concurrency;

namespace GameServer.UnitTests.Infrastructure.Concurrency;

public class LocalSemaphoreProviderTests
{
    private readonly LocalSemaphoreProvider _provider = new();

    [Fact]
    public async Task AcquireLockAsync_ShouldReturnDisposable()
    {
        var resourceId = Guid.NewGuid();

        using var lockHandle = await _provider.AcquireLockAsync(resourceId);

        Assert.NotNull(lockHandle);
    }

    [Fact]
    public async Task AcquireLockAsync_ShouldBlockConcurrentAccess()
    {
        var resourceId = Guid.NewGuid();
        var lockAcquired = false;
        var secondLockAttempted = false;

        using var firstLock = await _provider.AcquireLockAsync(resourceId);

        var secondLockTask = Task.Run(async () =>
        {
            secondLockAttempted = true;
            using var secondLock = await _provider.AcquireLockAsync(resourceId);
            lockAcquired = true;
        });

        await Task.Delay(100);
        Assert.True(secondLockAttempted);
        Assert.False(lockAcquired);

        firstLock.Dispose();
        await secondLockTask;

        Assert.True(lockAcquired);
    }

    [Fact]
    public async Task AcquireLocksAsync_ShouldAcquireBothLocks()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        using var lockHandle = await _provider.AcquireLocksAsync(id1, id2);

        Assert.NotNull(lockHandle);
    }

    [Fact]
    public async Task AcquireLocksAsync_ShouldAcquireInDeterministicOrder()
    {
        var smallerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var largerId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var firstLockAcquired = new TaskCompletionSource<bool>();
        var secondLockBlocked = new TaskCompletionSource<bool>();

        var task1 = Task.Run(async () =>
        {
            using var lock1 = await _provider.AcquireLockAsync(smallerId);
            firstLockAcquired.SetResult(true);
            await secondLockBlocked.Task;
        });

        await firstLockAcquired.Task;

        var lockAcquired = false;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        try
        {
            using var compositeLock = await _provider.AcquireLocksAsync(largerId, smallerId, cts.Token);
            lockAcquired = true;
        }
        catch (OperationCanceledException)
        {
            lockAcquired = false;
        }

        Assert.False(lockAcquired);

        secondLockBlocked.SetResult(true);
        await task1;
    }

    [Fact]
    public async Task DisposeLock_ShouldReleaseSemaphore()
    {
        var resourceId = Guid.NewGuid();

        var firstLock = await _provider.AcquireLockAsync(resourceId);
        firstLock.Dispose();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using var secondLock = await _provider.AcquireLockAsync(resourceId, cts.Token);

        Assert.NotNull(secondLock);
    }

    [Fact]
    public async Task DisposeLock_ShouldBeIdempotent()
    {
        var resourceId = Guid.NewGuid();

        var lockHandle = await _provider.AcquireLockAsync(resourceId);

        lockHandle.Dispose();
        lockHandle.Dispose();
        lockHandle.Dispose();

        using var secondLock = await _provider.AcquireLockAsync(resourceId);
        Assert.NotNull(secondLock);
    }

    [Fact]
    public async Task AcquireLocksAsync_DisposeShouldReleaseBothLocks()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var compositeLock = await _provider.AcquireLocksAsync(id1, id2);
        compositeLock.Dispose();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        using var lock1 = await _provider.AcquireLockAsync(id1, cts.Token);
        using var lock2 = await _provider.AcquireLockAsync(id2, cts.Token);

        Assert.NotNull(lock1);
        Assert.NotNull(lock2);
    }

    [Fact]
    public async Task AcquireLocksAsync_ShouldPreventDeadlock_WhenCalledWithReversedOrder()
    {
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var task1Started = new TaskCompletionSource<bool>();
        var task2Started = new TaskCompletionSource<bool>();

        var task1 = Task.Run(async () =>
        {
            task1Started.SetResult(true);
            await task2Started.Task;
            using var locks = await _provider.AcquireLocksAsync(id1, id2);
            return true;
        });

        var task2 = Task.Run(async () =>
        {
            task2Started.SetResult(true);
            await task1Started.Task;
            using var locks = await _provider.AcquireLocksAsync(id2, id1);
            return true;
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var completedTask = await Task.WhenAny(
            Task.WhenAll(task1, task2),
            Task.Delay(Timeout.Infinite, cts.Token));

        Assert.True(task1.IsCompletedSuccessfully || task2.IsCompletedSuccessfully);
    }
}

