using GameServer.Application.Common.Messages;
using GameServer.Application.Features.Social.Events;

namespace GameServer.Application.Features.Social;

public sealed class AddFriendHandler(
    IStateRepository stateRepository,
    ISessionManager sessionManager,
    IGameNotifier gameNotifier,
    ILogger<AddFriendHandler> logger) : IMessageHandler
{
    public async ValueTask<Result> HandleAsync(
        WebSocket webSocket,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        var playerId = sessionManager.GetPlayerId(webSocket);

        if (playerId is null)
        {
            return Result.Failure(new Error("Unauthorized", "Socket not registered. Please login first."));
        }

        AddFriendRequest request;

        try
        {
            request = payload.Deserialize<AddFriendRequest>(JsonSerializerOptionsProvider.Default);
        }
        catch (JsonException)
        {
            return Result.Failure(new Error("InvalidPayload", "Failed to deserialize add friend request"));
        }

        if (playerId.Value == request.FriendPlayerId)
        {
            return Result.Failure(new Error("InvalidFriend", "Cannot add yourself as a friend"));
        }

        var result = await stateRepository.AddFriendshipAsync(playerId.Value, request.FriendPlayerId, cancellationToken);

        if (!result.IsSuccess)
        {
            return Result.Failure(result.Error ?? new Error("AddFriendFailed", "Failed to add friend"));
        }
        if (sessionManager.IsPlayerOnline(request.FriendPlayerId))
        {
            var notification = new ServerMessage<FriendAddedPayload>(MessageTypes.FriendAdded, new FriendAddedPayload(playerId.Value));
            var messageBytes = JsonSerializer.SerializeToUtf8Bytes(notification, JsonSerializerOptionsProvider.Default);

            await gameNotifier.SendToPlayerAsync(
                request.FriendPlayerId,
                messageBytes,
                cancellationToken);
        }

        logger.FriendshipCreated(playerId.Value, request.FriendPlayerId);
        return Result.Success();
    }
}

