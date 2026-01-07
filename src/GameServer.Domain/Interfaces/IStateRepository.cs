namespace GameServer.Domain.Interfaces;

public interface IStateRepository
{
    Task<Result<Guid>> CreatePlayerAsync(string deviceId, CancellationToken cancellationToken = default);
    
    Task<Result<Guid>> GetPlayerIdByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default);
    
    Task<Result<long>> GetResourceAmountAsync(Guid playerId, ResourceType resourceType, CancellationToken cancellationToken = default);
    
    Task<Result> UpdateResourceAsync(Guid playerId, ResourceType resourceType, long amount, CancellationToken cancellationToken = default);
    
    Task<Result<IReadOnlyList<Guid>>> GetFriendIdsAsync(Guid playerId, CancellationToken cancellationToken = default);
    
    Task<Result> AddFriendshipAsync(Guid playerId1, Guid playerId2, CancellationToken cancellationToken = default);
    
    Task<Result> ExecuteInTransactionAsync(Func<Task<Result>> action, CancellationToken cancellationToken = default);
}

