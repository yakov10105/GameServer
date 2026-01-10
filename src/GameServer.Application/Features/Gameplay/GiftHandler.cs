using GameServer.Application.Common.Messages;
using GameServer.Application.Features.Gameplay.Events;

namespace GameServer.Application.Features.Gameplay;

public sealed class GiftHandler(
    IStateRepository stateRepository,
    ISessionManager sessionManager,
    ISynchronizationProvider synchronizationProvider,
    IGameNotifier gameNotifier,
    ILogger<GiftHandler> logger) : IMessageHandler
{
    public async ValueTask<Result> HandleAsync(
        WebSocket webSocket,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        var senderId = sessionManager.GetPlayerId(webSocket);
        
        if (senderId is null)
        {
            return Result.Failure(new Error("Unauthorized", "Socket not registered. Please login first."));
        }

        SendGiftRequest request;
        
        try
        {
            request = payload.Deserialize<SendGiftRequest>(JsonSerializerOptionsProvider.Default);
        }
        catch (JsonException)
        {
            return Result.Failure(new Error("InvalidPayload", "Failed to deserialize send gift request"));
        }

        if (senderId.Value == request.FriendPlayerId)
        {
            return Result.Failure(new Error("InvalidRecipient", "Cannot send gift to yourself"));
        }

        if (request.Value <= 0)
        {
            return Result.Failure(new Error("InvalidAmount", "Gift amount must be greater than zero"));
        }

        if (!Enum.IsDefined(request.Type))
        {
            return Result.Failure(new Error("InvalidResourceType", "Resource type must be Coins or Rolls"));
        }

        var friendsResult = await stateRepository.GetFriendIdsAsync(senderId.Value, cancellationToken);

        if (!friendsResult.IsSuccess || friendsResult.Value is null)
        {
            return Result.Failure(friendsResult.Error ?? new Error("GetFriendsFailed", "Failed to get friends list"));
        }

        if (!friendsResult.Value.Contains(request.FriendPlayerId))
        {
            logger.GiftToNonFriend(senderId.Value, request.FriendPlayerId);
            return Result.Failure(new Error("NotFriends", "You can only send gifts to friends"));
        }

        using var lockHandle = await synchronizationProvider.AcquireLocksAsync(
            senderId.Value, 
            request.FriendPlayerId, 
            cancellationToken);

        var senderBalanceResult = await stateRepository.GetResourceAmountAsync(
            senderId.Value, 
            request.Type, 
            cancellationToken);

        if (!senderBalanceResult.IsSuccess)
        {
            return Result.Failure(senderBalanceResult.Error ?? new Error("GetBalanceFailed", "Failed to get sender balance"));
        }

        if (senderBalanceResult.Value < request.Value)
        {
            logger.InsufficientFunds(senderId.Value, request.Type, senderBalanceResult.Value, request.Value);
            return Result.Failure(new Error("InsufficientFunds", $"Insufficient {request.Type}. Current: {senderBalanceResult.Value}, Requested: {request.Value}"));
        }

        var transactionResult = await stateRepository.ExecuteInTransactionAsync(async () =>
        {
            var deductResult = await stateRepository.UpdateResourceAsync(
                senderId.Value,
                request.Type,
                senderBalanceResult.Value - request.Value,
                cancellationToken);

            if (!deductResult.IsSuccess)
            {
                return Result.Failure(deductResult.Error ?? new Error("DeductFailed", "Failed to deduct from sender"));
            }

            var friendBalanceResult = await stateRepository.GetResourceAmountAsync(
                request.FriendPlayerId,
                request.Type,
                cancellationToken);

            if (!friendBalanceResult.IsSuccess)
            {
                return Result.Failure(friendBalanceResult.Error ?? new Error("GetFriendBalanceFailed", "Failed to get friend balance"));
            }

            var addResult = await stateRepository.UpdateResourceAsync(
                request.FriendPlayerId,
                request.Type,
                friendBalanceResult.Value + request.Value,
                cancellationToken);

            if (!addResult.IsSuccess)
            {
                return Result.Failure(addResult.Error ?? new Error("AddFailed", "Failed to add to friend"));
            }

            return Result.Success();
        }, cancellationToken);

        if (!transactionResult.IsSuccess)
        {
            return transactionResult;
        }

        if (sessionManager.IsPlayerOnline(request.FriendPlayerId))
        {
            var giftEvent = new ServerMessage<GiftReceivedPayload>(MessageTypes.GiftReceived, new(senderId.Value, request.Type, request.Value));
            var messageBytes = JsonSerializer.SerializeToUtf8Bytes(giftEvent, JsonSerializerOptionsProvider.Default);
            
            await gameNotifier.SendToPlayerAsync(
                request.FriendPlayerId, 
                messageBytes, 
                cancellationToken);
        }

        logger.GiftSent(senderId.Value, request.FriendPlayerId, request.Value, request.Type);
        return Result.Success();
    }
}
