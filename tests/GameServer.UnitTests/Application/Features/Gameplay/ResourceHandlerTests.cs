using System.Net.WebSockets;
using System.Text.Json;
using Moq;
using GameServer.Application.Features.Gameplay;
using GameServer.Domain.Common;
using GameServer.Domain.Enums;
using GameServer.Domain.Interfaces;

namespace GameServer.UnitTests.Application.Features.Gameplay;

public class ResourceHandlerTests
{
    private readonly Mock<IStateRepository> _mockRepository;
    private readonly Mock<ISessionManager> _mockSessionManager;
    private readonly Mock<WebSocket> _mockWebSocket;
    private readonly ResourceHandler _handler;

    public ResourceHandlerTests()
    {
        _mockRepository = new Mock<IStateRepository>();
        _mockSessionManager = new Mock<ISessionManager>();
        _mockWebSocket = new Mock<WebSocket>();
        _handler = new ResourceHandler(_mockRepository.Object, _mockSessionManager.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldDerivePlayerIdFromSocket_NotPayload()
    {
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { Type = ResourceType.Coins, Value = 100L });
        WebSocket? capturedSocket = null;

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Callback<WebSocket>(ws => capturedSocket = ws)
            .Returns(playerId);

        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(1000));

        _mockRepository.Setup(r => r.UpdateResourceAsync(playerId, ResourceType.Coins, 1100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.Same(_mockWebSocket.Object, capturedSocket);
        _mockSessionManager.Verify(s => s.GetPlayerId(_mockWebSocket.Object), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenSocketNotRegistered_ShouldReturnUnauthorizedError()
    {
        var payload = CreatePayload(new { Type = ResourceType.Coins, Value = 100L });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Returns((Guid?)null);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unauthorized", result.Error?.Code);
        _mockRepository.Verify(r => r.GetResourceAmountAsync(It.IsAny<Guid>(), It.IsAny<ResourceType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldUpdateBalanceCorrectly()
    {
        var playerId = Guid.NewGuid();
        var initialBalance = 1000L;
        var addValue = 500L;
        var expectedNewBalance = initialBalance + addValue;
        var payload = CreatePayload(new { Type = ResourceType.Coins, Value = addValue });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Returns(playerId);

        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(initialBalance));

        _mockRepository.Setup(r => r.UpdateResourceAsync(playerId, ResourceType.Coins, expectedNewBalance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(r => r.UpdateResourceAsync(playerId, ResourceType.Coins, expectedNewBalance, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithNegativeValue_ShouldDeductFromBalance()
    {
        var playerId = Guid.NewGuid();
        var initialBalance = 1000L;
        var deductValue = -200L;
        var expectedNewBalance = initialBalance + deductValue;
        var payload = CreatePayload(new { Type = ResourceType.Coins, Value = deductValue });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Returns(playerId);

        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(initialBalance));

        _mockRepository.Setup(r => r.UpdateResourceAsync(playerId, ResourceType.Coins, expectedNewBalance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(r => r.UpdateResourceAsync(playerId, ResourceType.Coins, 800, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenInsufficientFunds_ShouldReturnError()
    {
        var playerId = Guid.NewGuid();
        var initialBalance = 100L;
        var deductValue = -500L;
        var payload = CreatePayload(new { Type = ResourceType.Coins, Value = deductValue });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Returns(playerId);

        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(initialBalance));

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InsufficientFunds", result.Error?.Code);
        _mockRepository.Verify(r => r.UpdateResourceAsync(It.IsAny<Guid>(), It.IsAny<ResourceType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithMalformedPayload_ShouldReturnInvalidPayloadError()
    {
        var playerId = Guid.NewGuid();
        var malformedPayload = JsonDocument.Parse("[1, 2, 3]").RootElement;

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Returns(playerId);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, malformedPayload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InvalidPayload", result.Error?.Code);
    }

    [Theory]
    [InlineData(ResourceType.Coins)]
    [InlineData(ResourceType.Rolls)]
    public async Task HandleAsync_WithValidResourceTypes_ShouldSucceed(ResourceType resourceType)
    {
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { Type = resourceType, Value = 100L });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Returns(playerId);

        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(1000));

        _mockRepository.Setup(r => r.UpdateResourceAsync(playerId, resourceType, 1100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidResourceType_ShouldReturnError()
    {
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { Type = 999, Value = 100L });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Returns(playerId);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InvalidResourceType", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenGetResourceFails_ShouldReturnError()
    {
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { Type = ResourceType.Coins, Value = 100L });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Returns(playerId);

        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Failure(new Error("DatabaseError", "Connection failed")));

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("DatabaseError", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenUpdateResourceFails_ShouldReturnError()
    {
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { Type = ResourceType.Coins, Value = 100L });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Returns(playerId);

        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(1000));

        _mockRepository.Setup(r => r.UpdateResourceAsync(playerId, ResourceType.Coins, 1100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(new Error("DatabaseError", "Write failed")));

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("DatabaseError", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_ShouldPassCancellationTokenToRepository()
    {
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { Type = ResourceType.Coins, Value = 100L });
        using var cts = new CancellationTokenSource();
        CancellationToken capturedToken = default;

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Returns(playerId);

        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .Callback<Guid, ResourceType, CancellationToken>((_, _, ct) => capturedToken = ct)
            .ReturnsAsync(Result<long>.Success(1000));

        _mockRepository.Setup(r => r.UpdateResourceAsync(playerId, ResourceType.Coins, 1100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await _handler.HandleAsync(_mockWebSocket.Object, payload, cts.Token);

        Assert.Equal(cts.Token, capturedToken);
    }

    [Fact]
    public async Task HandleAsync_WithZeroBalance_ShouldAllowAddition()
    {
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { Type = ResourceType.Rolls, Value = 50L });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Returns(playerId);

        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, ResourceType.Rolls, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(0));

        _mockRepository.Setup(r => r.UpdateResourceAsync(playerId, ResourceType.Rolls, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_DeductExactBalance_ShouldSucceedWithZeroBalance()
    {
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { Type = ResourceType.Coins, Value = -1000L });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Returns(playerId);

        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(1000));

        _mockRepository.Setup(r => r.UpdateResourceAsync(playerId, ResourceType.Coins, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(r => r.UpdateResourceAsync(playerId, ResourceType.Coins, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static JsonElement CreatePayload(object data)
    {
        var json = JsonSerializer.Serialize(data);
        return JsonDocument.Parse(json).RootElement;
    }
}

