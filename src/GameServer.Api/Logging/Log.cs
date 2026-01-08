using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace GameServer.Api.Logging;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Player {PlayerId} logged in from device {DeviceId}")]
    public static partial void PlayerLoggedIn(this ILogger logger, Guid playerId, string deviceId);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Player {PlayerId} disconnected")]
    public static partial void PlayerDisconnected(this ILogger logger, Guid playerId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Player {PlayerId} updated {ResourceType}: {OldAmount} -> {NewAmount}")]
    public static partial void ResourceUpdated(this ILogger logger, Guid playerId, ResourceType resourceType, long oldAmount, long newAmount);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Player {SenderId} sent {Amount} {ResourceType} to Player {RecipientId}")]
    public static partial void GiftSent(this ILogger logger, Guid senderId, long amount, ResourceType resourceType, Guid recipientId);

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Warning,
        Message = "Player {PlayerId} attempted login while already connected")]
    public static partial void DuplicateLoginAttempt(this ILogger logger, Guid playerId);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Player {SenderId} attempted to send gift to non-friend {RecipientId}")]
    public static partial void GiftToNonFriend(this ILogger logger, Guid senderId, Guid recipientId);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Player {PlayerId} has insufficient {ResourceType}: required {Required}, has {Available}")]
    public static partial void InsufficientResources(this ILogger logger, Guid playerId, ResourceType resourceType, long required, long available);

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Error,
        Message = "WebSocket error for Player {PlayerId}: {ErrorMessage}")]
    public static partial void WebSocketError(this ILogger logger, Guid playerId, string errorMessage, Exception exception);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Error,
        Message = "Database operation failed: {Operation}")]
    public static partial void DatabaseOperationFailed(this ILogger logger, string operation, Exception exception);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Error,
        Message = "Message dispatch failed for type {MessageType}")]
    public static partial void MessageDispatchFailed(this ILogger logger, string messageType, Exception exception);

    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Debug,
        Message = "WebSocket message received from Player {PlayerId}: {MessageType}")]
    public static partial void MessageReceived(this ILogger logger, Guid playerId, string messageType);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Debug,
        Message = "Acquired lock for Player {PlayerId}")]
    public static partial void LockAcquired(this ILogger logger, Guid playerId);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Debug,
        Message = "Released lock for Player {PlayerId}")]
    public static partial void LockReleased(this ILogger logger, Guid playerId);

    [LoggerMessage(
    EventId = 5000,
    Level = LogLevel.Information,
    Message = "WebSocket accepted. Active connections: {ActiveConnections}")]
    public static partial void ConnectionAccepted(this ILogger logger, int activeConnections);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "Message received: {MessageType}, Size: {SizeBytes}, Latency: {LatencyMs}ms")]
    public static partial void MessageReceived(this ILogger logger, string messageType, int sizeBytes, double latencyMs);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Warning,
        Message = "Slow message processing: {MessageType} took {DurationMs}ms (threshold: {ThresholdMs}ms)")]
    public static partial void SlowMessageProcessing(this ILogger logger, string messageType, double durationMs, int thresholdMs);
}

