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

    [Fact]
    public void RemoveBySocket_WhenSessionExists_ShouldRemoveAndReturnPlayerId()
    {
        var playerId = Guid.NewGuid();
        var mockWebSocket = new Mock<WebSocket>();
        _sessionManager.RegisterSession(playerId, mockWebSocket.Object);

        var removedPlayerId = _sessionManager.RemoveBySocket(mockWebSocket.Object);

        Assert.Equal(playerId, removedPlayerId);
        Assert.False(_sessionManager.IsPlayerOnline(playerId));
        Assert.Null(_sessionManager.GetSocket(playerId));
        Assert.Null(_sessionManager.GetPlayerId(mockWebSocket.Object));
    }

    [Fact]
    public void RemoveBySocket_WhenSessionDoesNotExist_ShouldReturnNull()
    {
        var unregisteredSocket = new Mock<WebSocket>();

        var removedPlayerId = _sessionManager.RemoveBySocket(unregisteredSocket.Object);

        Assert.Null(removedPlayerId);
    }

    [Fact]
    public void RemoveBySocket_ThenReRegister_ShouldAllowReLogin()
    {
        var playerId = Guid.NewGuid();
        var originalSocket = new Mock<WebSocket>();
        var newSocket = new Mock<WebSocket>();
        _sessionManager.RegisterSession(playerId, originalSocket.Object);

        _sessionManager.RemoveBySocket(originalSocket.Object);
        _sessionManager.RegisterSession(playerId, newSocket.Object);

        Assert.True(_sessionManager.IsPlayerOnline(playerId));
        Assert.Same(newSocket.Object, _sessionManager.GetSocket(playerId));
    }
}
