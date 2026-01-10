using GameServer.Application.Common.Messages;
using GameServer.Application.Features.Gameplay.Responses;

namespace GameServer.Application.Features.Gameplay;

public sealed class ResourceHandler(
    IStateRepository stateRepository,
    ISessionManager sessionManager,
    IGameNotifier gameNotifier,
    ILogger<ResourceHandler> logger) : IMessageHandler
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

        UpdateResourceRequest request;
        
        try
        {
            request = payload.Deserialize<UpdateResourceRequest>(JsonSerializerOptionsProvider.Default);
        }
        catch (JsonException)
        {
            return Result.Failure(new Error("InvalidPayload", "Failed to deserialize update resource request"));
        }

        if (!Enum.IsDefined(request.Type))
        {
            return Result.Failure(new Error("InvalidResourceType", "Resource type must be Coins or Rolls"));
        }

        var currentBalanceResult = await stateRepository.GetResourceAmountAsync(
            playerId.Value, 
            request.Type, 
            cancellationToken);

        if (!currentBalanceResult.IsSuccess)
        {
            return Result.Failure(currentBalanceResult.Error ?? new Error("GetResourceFailed", "Failed to get current balance"));
        }

        var oldBalance = currentBalanceResult.Value;
        var newBalance = oldBalance + request.Value;

        if (newBalance < 0)
        {
            logger.InsufficientFunds(playerId.Value, request.Type, oldBalance, -request.Value);
            return Result.Failure(new Error("InsufficientFunds", $"Insufficient {request.Type}. Current: {oldBalance}, Requested: {request.Value}"));
        }

        var updateResult = await stateRepository.UpdateResourceAsync(
            playerId.Value,
            request.Type,
            newBalance,
            cancellationToken);

        if (!updateResult.IsSuccess)
        {
            return Result.Failure(updateResult.Error ?? new Error("UpdateResourceFailed", "Failed to update resource"));
        }

        var response = new ServerMessage<ResourceUpdatedPayload>(
            MessageTypes.ResourceUpdated,
            new ResourceUpdatedPayload(request.Type, newBalance));
        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(response, JsonSerializerOptionsProvider.Default);
        await gameNotifier.SendToPlayerAsync(playerId.Value, messageBytes, cancellationToken);

        logger.ResourceUpdated(playerId.Value, request.Type, oldBalance, newBalance);
        return Result.Success();
    }
}
