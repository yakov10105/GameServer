using Microsoft.Extensions.Logging;

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
}

