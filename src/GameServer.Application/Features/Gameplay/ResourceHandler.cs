using GameServer.Domain.Enums;
using GameServer.Domain.Interfaces;
using System.Text.Json;

namespace GameServer.Application.Features.Gameplay;

public sealed class ResourceHandler(
    IStateRepository stateRepository,
    ISessionManager sessionManager) : IMessageHandler
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
            request = payload.Deserialize<UpdateResourceRequest>();
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

        var newBalance = currentBalanceResult.Value + request.Value;

        if (newBalance < 0)
        {
            return Result.Failure(new Error("InsufficientFunds", $"Insufficient {request.Type}. Current: {currentBalanceResult.Value}, Requested: {request.Value}"));
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

        return Result.Success();
    }
}
