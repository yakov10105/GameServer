namespace GameServer.Infrastructure.Logging;

public static partial class InfrastructureLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug,
        Message = "Session registered: Player {PlayerId}")]
    public static partial void SessionRegistered(this ILogger logger, Guid playerId);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug,
        Message = "Session removed: Player {PlayerId}")]
    public static partial void SessionRemoved(this ILogger logger, Guid playerId);

    [LoggerMessage(EventId = 2100, Level = LogLevel.Debug,
        Message = "Player created: {PlayerId} (DeviceId: {DeviceId})")]
    public static partial void PlayerCreated(this ILogger logger, Guid playerId, string deviceId);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Debug,
        Message = "Friendship added: {PlayerId1} <-> {PlayerId2}")]
    public static partial void FriendshipAdded(this ILogger logger, Guid playerId1, Guid playerId2);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Warning,
        Message = "Database error in {Operation}: {ErrorMessage}")]
    public static partial void DatabaseError(this ILogger logger, string operation, string errorMessage);

    [LoggerMessage(EventId = 2200, Level = LogLevel.Debug,
        Message = "Lock acquisition started for resource: {ResourceId}")]
    public static partial void LockAcquisitionStarted(this ILogger logger, Guid resourceId);

    [LoggerMessage(EventId = 2201, Level = LogLevel.Debug,
        Message = "Lock acquired successfully for resource: {ResourceId}")]
    public static partial void LockAcquired(this ILogger logger, Guid resourceId);

    [LoggerMessage(EventId = 2204, Level = LogLevel.Debug,
        Message = "Dual lock acquisition started: {FirstId} (Primary) and {SecondId} (Secondary)")]
    public static partial void DualLockAcquisitionStarted(this ILogger logger, Guid firstId, Guid secondId);

    [LoggerMessage(EventId = 2205, Level = LogLevel.Warning,
        Message = "Dual lock second acquisition failed/cancelled. Rolling back first lock for {FirstId}")]
    public static partial void DualLockRollback(this ILogger logger, Guid firstId);
}

