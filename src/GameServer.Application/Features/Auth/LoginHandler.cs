using GameServer.Domain.Interfaces;

namespace GameServer.Application.Features.Auth;

public sealed class LoginHandler(
    IStateRepository stateRepository,
    ISessionManager sessionManager,
    IGameNotifier gameNotifier) : IMessageHandler
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
            return Result.Failure(new Error("AlreadyOnline", "Player is already connected from another device"));
        }

        sessionManager.RegisterSession(playerId, webSocket);

        var response = new { type = "LOGIN_RESPONSE", payload = new { playerId } };
        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(response, JsonSerializerOptionsProvider.Default);
        await gameNotifier.SendToPlayerAsync(playerId, messageBytes, cancellationToken);

        return Result.Success();
    }
}

