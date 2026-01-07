namespace GameServer.Domain.Interfaces;

public interface ISynchronizationProvider
{
    Task<IDisposable> AcquireLockAsync(Guid resourceId, CancellationToken cancellationToken = default);
    
    Task<IDisposable> AcquireLocksAsync(Guid resourceId1, Guid resourceId2, CancellationToken cancellationToken = default);
}

