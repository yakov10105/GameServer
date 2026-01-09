using System.Text.Json;
using GameServer.Application.Features.Gameplay;
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

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(playerId);
        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(1000));
        _mockRepository.Setup(r => r.UpdateResourceAsync(playerId, ResourceType.Coins, 1100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        _mockSessionManager.Verify(s => s.GetPlayerId(_mockWebSocket.Object), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenSocketNotRegistered_ShouldReturnUnauthorizedError()
    {
        var payload = CreatePayload(new { Type = ResourceType.Coins, Value = 100L });
        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns((Guid?)null);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unauthorized", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldUpdateBalanceCorrectly()
    {
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { Type = ResourceType.Coins, Value = 500L });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(playerId);
        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(1000));
        _mockRepository.Setup(r => r.UpdateResourceAsync(playerId, ResourceType.Coins, 1500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(r => r.UpdateResourceAsync(playerId, ResourceType.Coins, 1500, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenInsufficientFunds_ShouldReturnError()
    {
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { Type = ResourceType.Coins, Value = -500L });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(playerId);
        _mockRepository.Setup(r => r.GetResourceAmountAsync(playerId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(100));

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InsufficientFunds", result.Error?.Code);
        _mockRepository.Verify(r => r.UpdateResourceAsync(It.IsAny<Guid>(), It.IsAny<ResourceType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidResourceType_ShouldReturnError()
    {
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { Type = 999, Value = 100L });

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(playerId);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InvalidResourceType", result.Error?.Code);
    }

    private static JsonElement CreatePayload(object data)
    {
        var json = JsonSerializer.Serialize(data);
        return JsonDocument.Parse(json).RootElement;
    }
}
