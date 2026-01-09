using GameServer.Domain.Interfaces;
using System.Text.Json;

namespace GameServer.Application.Features.Auth;

public sealed class LoginHandler(
    IStateRepository stateRepository,
    ISessionManager sessionManager) : IMessageHandler
{
    public async ValueTask<Result> HandleAsync(
        WebSocket webSocket,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        LoginRequest request;
        
        try
        {
            request = payload.Deserialize<LoginRequest>();
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
            return Result.Failure(new Error("AlreadyOnline", "Player is already connected from another device"));
        }

        sessionManager.RegisterSession(playerId, webSocket);

        return Result.Success();
    }
}

