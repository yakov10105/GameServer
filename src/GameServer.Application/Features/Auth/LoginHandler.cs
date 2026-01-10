using GameServer.Application.Common.Messages;
using GameServer.Application.Features.Auth.Responses;
using GameServer.Application.Features.Social.Events;

namespace GameServer.Application.Features.Auth;

public sealed class LoginHandler(
    IStateRepository stateRepository,
    ISessionManager sessionManager,
    IGameNotifier gameNotifier,
    ILogger<LoginHandler> logger) : IMessageHandler
{
    public async ValueTask<Result> HandleAsync(
        WebSocket webSocket,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        LoginRequest request;

        try
        {
            request = payload.Deserialize<LoginRequest>(JsonSerializerOptionsProvider.Default);
        }
        catch (JsonException)
        {
            return Result.Failure(new Error("InvalidPayload", "Failed to deserialize login request"));
        }

        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return Result.Failure(new Error("InvalidDeviceId", "DeviceId is required"));
        }

        var playerIdResult = await stateRepository.GetPlayerIdByDeviceIdAsync(request.DeviceId, cancellationToken);

        Guid playerId;

        if (!playerIdResult.IsSuccess)
        {
            var createResult = await stateRepository.CreatePlayerAsync(request.DeviceId, cancellationToken);

            if (!createResult.IsSuccess)
            {
                return Result.Failure(createResult.Error ?? new Error("CreatePlayerFailed", "Failed to create player"));
            }

            playerId = createResult.Value;
        }
        else
        {
            playerId = playerIdResult.Value;
        }

        if (sessionManager.IsPlayerOnline(playerId))
        {
            logger.DuplicateLoginAttempt(playerId);
            return Result.Failure(new Error("AlreadyOnline", "Player is already connected from another device"));
        }

        sessionManager.RegisterSession(playerId, webSocket);

        var response = new ServerMessage<LoginResponsePayload>(MessageTypes.LoginResponse, new(playerId));
        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(response, JsonSerializerOptionsProvider.Default);
        await gameNotifier.SendToPlayerAsync(playerId, messageBytes, cancellationToken);

        logger.PlayerLoggedIn(playerId, request.DeviceId);

        await NotifyFriendsPlayerOnlineAsync(playerId, cancellationToken);

        return Result.Success();
    }

    private async Task NotifyFriendsPlayerOnlineAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var friendsResult = await stateRepository.GetFriendIdsAsync(playerId, cancellationToken);

        if (!friendsResult.IsSuccess || friendsResult.Value is not { Count: > 0 } friends)
        {
            return;
        }

        var notification = new ServerMessage<FriendOnlinePayload>(
            MessageTypes.FriendOnline,
            new FriendOnlinePayload(playerId));
        var notificationBytes = JsonSerializer.SerializeToUtf8Bytes(notification, JsonSerializerOptionsProvider.Default);

        foreach (var friendId in friends)
        {
            if (sessionManager.IsPlayerOnline(friendId))
            {
                await gameNotifier.SendToPlayerAsync(friendId, notificationBytes, cancellationToken);
                logger.FriendOnlineNotificationSent(playerId, friendId);
            }
        }
    }
}

