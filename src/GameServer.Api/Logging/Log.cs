using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace GameServer.Api.Logging;

public static partial class Log
{

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
        Message = "Message received - Size: {SizeBytes}, Latency: {LatencyMs}ms")]
    public static partial void MessageReceived(this ILogger logger, int sizeBytes, double latencyMs);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Warning,
        Message = "Slow message processing - took {DurationMs}ms (threshold: {ThresholdMs}ms)")]
    public static partial void SlowMessageProcessing(this ILogger logger, double durationMs, int thresholdMs);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Information,
        Message = "Session cleaned up for Player {PlayerId} on disconnect")]
    public static partial void SessionCleanedUp(this ILogger logger, Guid playerId);
}

