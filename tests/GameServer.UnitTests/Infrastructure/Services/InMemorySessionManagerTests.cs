namespace GameServer.UnitTests.Infrastructure.Services;

public sealed class InMemorySessionManagerTests
{
    [Fact]
    public void RegisterSession_ShouldAddSessionAndAllowRetrieval()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();
        var mockWebSocket = new Mock<WebSocket>();

        sessionManager.RegisterSession(playerId, mockWebSocket.Object);

        Assert.True(sessionManager.IsPlayerOnline(playerId));
        Assert.Same(mockWebSocket.Object, sessionManager.GetSocket(playerId));
        Assert.Equal(playerId, sessionManager.GetPlayerId(mockWebSocket.Object));
    }

    [Fact]
    public void GetPlayerId_WhenSocketNotRegistered_ShouldReturnNull()
    {
        var sessionManager = new InMemorySessionManager();
        var mockWebSocket = new Mock<WebSocket>();

        var playerId = sessionManager.GetPlayerId(mockWebSocket.Object);

        Assert.Null(playerId);
    }

    [Fact]
    public void RemoveSession_ShouldRemoveBothMappings()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();
        var mockWebSocket = new Mock<WebSocket>();
        sessionManager.RegisterSession(playerId, mockWebSocket.Object);

        sessionManager.RemoveSession(playerId);

        Assert.False(sessionManager.IsPlayerOnline(playerId));
        Assert.Null(sessionManager.GetSocket(playerId));
        Assert.Null(sessionManager.GetPlayerId(mockWebSocket.Object));
    }

    [Fact]
    public void ConcurrentAccess_WithMultipleRegistrations_ShouldHandleAllSafely()
    {
        var sessionManager = new InMemorySessionManager();
        var playerCount = 100;
        var playerIds = Enumerable.Range(0, playerCount)
                                   .Select(_ => Guid.NewGuid())
                                   .ToList();

        Parallel.ForEach(playerIds, playerId =>
        {
            var mockWebSocket = new Mock<WebSocket>();
            sessionManager.RegisterSession(playerId, mockWebSocket.Object);
        });

        foreach (var playerId in playerIds)
        {
            Assert.True(sessionManager.IsPlayerOnline(playerId));
        }
    }
}
