namespace GameServer.Infrastructure.Network;

public sealed class WebSocketDirectNotifier(
    ISessionManager sessionManager,
    ILogger<WebSocketDirectNotifier> logger)
    : IGameNotifier
{
    public async Task BroadcastAsync(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default)
    {
        var playerIds = sessionManager.GetAllPlayerIds();
        var tasks = new Task[playerIds.Count];
        var index = 0;

        foreach (var playerId in playerIds)
        {
            tasks[index++] = InternalSendAsync(messageBytes, playerId, cancellationToken);
        }

        await Task.WhenAll(tasks);
    }

    public async Task BroadcastExceptAsync(Guid excludePlayerId, ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default)
    {
        var playerIds = sessionManager.GetAllPlayerIds();
        var tasks = new List<Task>(playerIds.Count);

        foreach (var playerId in playerIds)
        {
            if (playerId != excludePlayerId)
            {
                tasks.Add(InternalSendAsync(messageBytes, playerId, cancellationToken));
            }
        }

        await Task.WhenAll(tasks);
    }

    public async Task SendToPlayerAsync(Guid playerId, ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default)
    {
        await InternalSendAsync(messageBytes, playerId, cancellationToken);
    }

    private async Task InternalSendAsync(ReadOnlyMemory<byte> messageBytes, Guid playerId, CancellationToken cancellationToken)
    {
        var socket = sessionManager.GetSocket(playerId);
        if (socket is null || socket.State is not WebSocketState.Open) 
            return;

        try
        {
            await socket.SendAsync(messageBytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send message to player {PlayerId}", playerId);
        }
    }
}