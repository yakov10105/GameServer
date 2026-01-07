namespace GameServer.UnitTests.Infrastructure.Services;

public sealed class InMemorySessionManagerTests
{
    [Fact]
    public void RegisterSession_WhenPlayerNotRegistered_ShouldAddSession()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();
        var mockWebSocket = new Mock<WebSocket>();

        sessionManager.RegisterSession(playerId, mockWebSocket.Object);

        var isOnline = sessionManager.IsPlayerOnline(playerId);
        Assert.True(isOnline);
    }

    [Fact]
    public void RegisterSession_WhenCalled_ShouldAllowRetrievalByPlayerId()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();
        var mockWebSocket = new Mock<WebSocket>();

        sessionManager.RegisterSession(playerId, mockWebSocket.Object);

        var retrievedSocket = sessionManager.GetSocket(playerId);
        Assert.NotNull(retrievedSocket);
        Assert.Same(mockWebSocket.Object, retrievedSocket);
    }

    [Fact]
    public void RegisterSession_WhenCalled_ShouldAllowRetrievalByWebSocket()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();
        var mockWebSocket = new Mock<WebSocket>();

        sessionManager.RegisterSession(playerId, mockWebSocket.Object);

        var retrievedPlayerId = sessionManager.GetPlayerId(mockWebSocket.Object);
        Assert.NotNull(retrievedPlayerId);
        Assert.Equal(playerId, retrievedPlayerId.Value);
    }

    [Fact]
    public void GetPlayerId_WhenSocketRegistered_ShouldReturnCorrectId()
    {
        var sessionManager = new InMemorySessionManager();
        var expectedPlayerId = Guid.NewGuid();
        var mockWebSocket = new Mock<WebSocket>();
        sessionManager.RegisterSession(expectedPlayerId, mockWebSocket.Object);

        var actualPlayerId = sessionManager.GetPlayerId(mockWebSocket.Object);

        Assert.NotNull(actualPlayerId);
        Assert.Equal(expectedPlayerId, actualPlayerId.Value);
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
    public void GetSocket_WhenPlayerRegistered_ShouldReturnCorrectSocket()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();
        var expectedWebSocket = new Mock<WebSocket>().Object;
        sessionManager.RegisterSession(playerId, expectedWebSocket);

        var actualWebSocket = sessionManager.GetSocket(playerId);

        Assert.NotNull(actualWebSocket);
        Assert.Same(expectedWebSocket, actualWebSocket);
    }

    [Fact]
    public void GetSocket_WhenPlayerNotRegistered_ShouldReturnNull()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();

        var webSocket = sessionManager.GetSocket(playerId);

        Assert.Null(webSocket);
    }

    [Fact]
    public void IsPlayerOnline_WhenPlayerRegistered_ShouldReturnTrue()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();
        var mockWebSocket = new Mock<WebSocket>();
        sessionManager.RegisterSession(playerId, mockWebSocket.Object);

        var isOnline = sessionManager.IsPlayerOnline(playerId);

        Assert.True(isOnline);
    }

    [Fact]
    public void IsPlayerOnline_WhenPlayerNotRegistered_ShouldReturnFalse()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();

        var isOnline = sessionManager.IsPlayerOnline(playerId);

        Assert.False(isOnline);
    }

    [Fact]
    public void RemoveSession_WhenPlayerRegistered_ShouldRemoveSession()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();
        var mockWebSocket = new Mock<WebSocket>();
        sessionManager.RegisterSession(playerId, mockWebSocket.Object);

        sessionManager.RemoveSession(playerId);

        var isOnline = sessionManager.IsPlayerOnline(playerId);
        Assert.False(isOnline);
    }

    [Fact]
    public void RemoveSession_WhenPlayerRegistered_ShouldRemoveBothMappings()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();
        var mockWebSocket = new Mock<WebSocket>();
        sessionManager.RegisterSession(playerId, mockWebSocket.Object);

        sessionManager.RemoveSession(playerId);

        var retrievedSocket = sessionManager.GetSocket(playerId);
        var retrievedPlayerId = sessionManager.GetPlayerId(mockWebSocket.Object);
        Assert.Null(retrievedSocket);
        Assert.Null(retrievedPlayerId);
    }

    [Fact]
    public void RemoveSession_WhenPlayerNotRegistered_ShouldNotThrow()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();

        var exception = Record.Exception(() => sessionManager.RemoveSession(playerId));

        Assert.Null(exception);
    }

    [Fact]
    public void RegisterSession_WhenPlayerReconnects_ShouldUpdateSession()
    {
        var sessionManager = new InMemorySessionManager();
        var playerId = Guid.NewGuid();
        var firstSocket = new Mock<WebSocket>().Object;
        var secondSocket = new Mock<WebSocket>().Object;

        sessionManager.RegisterSession(playerId, firstSocket);
        sessionManager.RegisterSession(playerId, secondSocket);

        var retrievedSocket = sessionManager.GetSocket(playerId);
        var retrievedPlayerId = sessionManager.GetPlayerId(secondSocket);
        Assert.Same(secondSocket, retrievedSocket);
        Assert.Equal(playerId, retrievedPlayerId);
    }

    [Fact]
    public void ConcurrentAccess_WithMultipleRegistrations_ShouldHandleAllSafely()
    {
        var sessionManager = new InMemorySessionManager();
        var playerCount = 1000;
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
            var isOnline = sessionManager.IsPlayerOnline(playerId);
            Assert.True(isOnline);
        }
    }

    [Fact]
    public void ConcurrentAccess_WithMixedOperations_ShouldMaintainConsistency()
    {
        var sessionManager = new InMemorySessionManager();
        var playerCount = 500;
        var playerIds = Enumerable.Range(0, playerCount)
                                   .Select(_ => Guid.NewGuid())
                                   .ToArray();

        Parallel.For(0, playerCount, i =>
        {
            var playerId = playerIds[i];
            var mockWebSocket = new Mock<WebSocket>();
            
            sessionManager.RegisterSession(playerId, mockWebSocket.Object);
            
            var isOnline = sessionManager.IsPlayerOnline(playerId);
            Assert.True(isOnline);
            
            var retrievedSocket = sessionManager.GetSocket(playerId);
            Assert.NotNull(retrievedSocket);
            
            var retrievedPlayerId = sessionManager.GetPlayerId(mockWebSocket.Object);
            Assert.Equal(playerId, retrievedPlayerId);
        });

        var onlineCount = playerIds.Count(playerId => sessionManager.IsPlayerOnline(playerId));
        Assert.Equal(playerCount, onlineCount);
    }

    [Fact]
    public void ConcurrentAccess_WithRegistrationAndRemoval_ShouldHandleRaceSafely()
    {
        var sessionManager = new InMemorySessionManager();
        var playerCount = 500;
        var playerIds = Enumerable.Range(0, playerCount)
                                   .Select(_ => Guid.NewGuid())
                                   .ToArray();

        Parallel.For(0, playerCount, i =>
        {
            var playerId = playerIds[i];
            var mockWebSocket = new Mock<WebSocket>();
            
            sessionManager.RegisterSession(playerId, mockWebSocket.Object);
            
            if (i % 2 == 0)
            {
                sessionManager.RemoveSession(playerId);
            }
        });

        var expectedOnlineCount = playerCount / 2;
        var actualOnlineCount = playerIds.Count(playerId => sessionManager.IsPlayerOnline(playerId));
        
        Assert.True(actualOnlineCount >= expectedOnlineCount - 10 && actualOnlineCount <= expectedOnlineCount + 10);
    }

    [Fact]
    public void RegisterSession_WithDifferentPlayers_ShouldMaintainSeparateSessions()
    {
        var sessionManager = new InMemorySessionManager();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var socket1 = new Mock<WebSocket>().Object;
        var socket2 = new Mock<WebSocket>().Object;

        sessionManager.RegisterSession(player1Id, socket1);
        sessionManager.RegisterSession(player2Id, socket2);

        var retrievedSocket1 = sessionManager.GetSocket(player1Id);
        var retrievedSocket2 = sessionManager.GetSocket(player2Id);
        var retrievedPlayer1 = sessionManager.GetPlayerId(socket1);
        var retrievedPlayer2 = sessionManager.GetPlayerId(socket2);

        Assert.Same(socket1, retrievedSocket1);
        Assert.Same(socket2, retrievedSocket2);
        Assert.Equal(player1Id, retrievedPlayer1);
        Assert.Equal(player2Id, retrievedPlayer2);
    }
}

