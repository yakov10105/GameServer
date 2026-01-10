using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.UnitTests.Infrastructure.Services;

public sealed class InMemorySessionManagerTests
{
    private readonly InMemorySessionManager _sessionManager = new(
        NullLogger<InMemorySessionManager>.Instance);

    [Fact]
    public void RegisterSession_ShouldAddSessionAndAllowRetrieval()
    {
        var playerId = Guid.NewGuid();
        var mockWebSocket = new Mock<WebSocket>();

        _sessionManager.RegisterSession(playerId, mockWebSocket.Object);

        Assert.True(_sessionManager.IsPlayerOnline(playerId));
        Assert.Same(mockWebSocket.Object, _sessionManager.GetSocket(playerId));
        Assert.Equal(playerId, _sessionManager.GetPlayerId(mockWebSocket.Object));
    }

    [Fact]
    public void GetPlayerId_WhenSocketNotRegistered_ShouldReturnNull()
    {
        var mockWebSocket = new Mock<WebSocket>();

        var playerId = _sessionManager.GetPlayerId(mockWebSocket.Object);

        Assert.Null(playerId);
    }

    [Fact]
    public void RemoveSession_ShouldRemoveBothMappings()
    {
        var playerId = Guid.NewGuid();
        var mockWebSocket = new Mock<WebSocket>();
        _sessionManager.RegisterSession(playerId, mockWebSocket.Object);

        _sessionManager.RemoveSession(playerId);

        Assert.False(_sessionManager.IsPlayerOnline(playerId));
        Assert.Null(_sessionManager.GetSocket(playerId));
        Assert.Null(_sessionManager.GetPlayerId(mockWebSocket.Object));
    }

    [Fact]
    public void ConcurrentAccess_WithMultipleRegistrations_ShouldHandleAllSafely()
    {
        var sessionManager = new InMemorySessionManager(NullLogger<InMemorySessionManager>.Instance);
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
