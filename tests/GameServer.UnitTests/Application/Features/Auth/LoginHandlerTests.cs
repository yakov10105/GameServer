using System.Net.WebSockets;
using System.Text.Json;
using Moq;
using GameServer.Application.Features.Auth;
using GameServer.Domain.Common;
using GameServer.Domain.Interfaces;

namespace GameServer.UnitTests.Application.Features.Auth;

public class LoginHandlerTests
{
    private readonly Mock<IStateRepository> _mockRepository;
    private readonly Mock<ISessionManager> _mockSessionManager;
    private readonly Mock<WebSocket> _mockWebSocket;
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _mockRepository = new Mock<IStateRepository>();
        _mockSessionManager = new Mock<ISessionManager>();
        _mockWebSocket = new Mock<WebSocket>();
        _handler = new LoginHandler(_mockRepository.Object, _mockSessionManager.Object);
    }

    [Fact]
    public async Task HandleAsync_WithNewPlayer_ShouldCreatePlayerAndRegisterSession()
    {
        var deviceId = "new-device-123";
        var newPlayerId = Guid.NewGuid();
        var payload = CreatePayload(new { DeviceId = deviceId });

        _mockRepository.Setup(r => r.GetPlayerIdByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Failure(new Error("NotFound", "Player not found")));
        _mockRepository.Setup(r => r.CreatePlayerAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(newPlayerId));
        _mockSessionManager.Setup(s => s.IsPlayerOnline(newPlayerId)).Returns(false);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(r => r.CreatePlayerAsync(deviceId, It.IsAny<CancellationToken>()), Times.Once);
        _mockSessionManager.Verify(s => s.RegisterSession(newPlayerId, _mockWebSocket.Object), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithExistingPlayer_ShouldNotCreateNewPlayer()
    {
        var deviceId = "existing-device-456";
        var existingPlayerId = Guid.NewGuid();
        var payload = CreatePayload(new { DeviceId = deviceId });

        _mockRepository.Setup(r => r.GetPlayerIdByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(existingPlayerId));
        _mockSessionManager.Setup(s => s.IsPlayerOnline(existingPlayerId)).Returns(false);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(r => r.CreatePlayerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenPlayerAlreadyOnline_ShouldReturnError()
    {
        var deviceId = "device-online";
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { DeviceId = deviceId });

        _mockRepository.Setup(r => r.GetPlayerIdByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(playerId));
        _mockSessionManager.Setup(s => s.IsPlayerOnline(playerId)).Returns(true);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("AlreadyOnline", result.Error?.Code);
        _mockSessionManager.Verify(s => s.RegisterSession(It.IsAny<Guid>(), It.IsAny<WebSocket>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyDeviceId_ShouldReturnInvalidDeviceIdError()
    {
        var payload = CreatePayload(new { DeviceId = "" });

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InvalidDeviceId", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_ShouldPassWebSocketToSessionManager()
    {
        var deviceId = "socket-test-device";
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(new { DeviceId = deviceId });
        WebSocket? capturedSocket = null;

        _mockRepository.Setup(r => r.GetPlayerIdByDeviceIdAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(playerId));
        _mockSessionManager.Setup(s => s.IsPlayerOnline(playerId)).Returns(false);
        _mockSessionManager.Setup(s => s.RegisterSession(playerId, It.IsAny<WebSocket>()))
            .Callback<Guid, WebSocket>((_, ws) => capturedSocket = ws);

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.Same(_mockWebSocket.Object, capturedSocket);
    }

    private static JsonElement CreatePayload(object data)
    {
        var json = JsonSerializer.Serialize(data);
        return JsonDocument.Parse(json).RootElement;
    }
}
