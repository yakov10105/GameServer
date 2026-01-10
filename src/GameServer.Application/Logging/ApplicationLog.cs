using GameServer.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Logging;

public static partial class ApplicationLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
        Message = "Player {PlayerId} logged in (DeviceId: {DeviceId})")]
    public static partial void PlayerLoggedIn(this ILogger logger, Guid playerId, string deviceId);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning,
        Message = "Duplicate login attempt for Player {PlayerId}")]
    public static partial void DuplicateLoginAttempt(this ILogger logger, Guid playerId);

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information,
        Message = "Player {PlayerId} updated {ResourceType}: {OldBalance} -> {NewBalance}")]
    public static partial void ResourceUpdated(this ILogger logger, Guid playerId, ResourceType resourceType, long oldBalance, long newBalance);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning,
        Message = "Player {PlayerId} insufficient {ResourceType}: has {Available}, needs {Required}")]
    public static partial void InsufficientFunds(this ILogger logger, Guid playerId, ResourceType resourceType, long available, long required);

    [LoggerMessage(EventId = 1200, Level = LogLevel.Information,
        Message = "Gift sent: {SenderId} -> {RecipientId}, {Amount} {ResourceType}")]
    public static partial void GiftSent(this ILogger logger, Guid senderId, Guid recipientId, long amount, ResourceType resourceType);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Warning,
        Message = "Gift to non-friend blocked: {SenderId} -> {RecipientId}")]
    public static partial void GiftToNonFriend(this ILogger logger, Guid senderId, Guid recipientId);

    [LoggerMessage(EventId = 1300, Level = LogLevel.Information,
        Message = "Friendship created: {PlayerId} <-> {FriendId}")]
    public static partial void FriendshipCreated(this ILogger logger, Guid playerId, Guid friendId);

    [LoggerMessage(EventId = 1400, Level = LogLevel.Debug,
        Message = "Friend online notification sent: Player {PlayerId} is online, notified {FriendId}")]
    public static partial void FriendOnlineNotificationSent(this ILogger logger, Guid playerId, Guid friendId);
}

