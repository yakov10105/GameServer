using System.Text;
using System.Text.Json;
using Moq;
using GameServer.Application.Services;
using GameServer.Application.Common.Interfaces;
using GameServer.Application.Common;
using System.Net.WebSockets;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.UnitTests.Application.Services;

public class MessageDispatcherTests
{
    private readonly TestServiceProvider _serviceProvider;
    private readonly Mock<IMessageHandler> _mockLoginHandler;
    private readonly Mock<IMessageHandler> _mockResourceHandler;
    private readonly Mock<WebSocket> _mockWebSocket;
    private readonly MessageDispatcher _dispatcher;

    public MessageDispatcherTests()
    {
        _serviceProvider = new TestServiceProvider();
        _mockLoginHandler = new Mock<IMessageHandler>();
        _mockResourceHandler = new Mock<IMessageHandler>();
        _mockWebSocket = new Mock<WebSocket>();
        _dispatcher = new MessageDispatcher(_serviceProvider);
    }

    [Fact]
    public async Task DispatchAsync_WhenMessageIsEmpty_ShouldReturnImmediately()
    {
        var emptyMessage = ReadOnlyMemory<byte>.Empty;

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, emptyMessage, CancellationToken.None);

        Assert.Empty(_serviceProvider.RequestedKeys);
    }

    [Fact]
    public async Task DispatchAsync_WithValidLoginMessage_ShouldInvokeLoginHandler()
    {
        var loginMessage = CreateMessage("LOGIN", new { deviceId = "device123" });
        _mockLoginHandler.Setup(h => h.HandleAsync(
            It.IsAny<WebSocket>(),
            It.IsAny<JsonElement>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _serviceProvider.RegisterKeyedService("LOGIN", _mockLoginHandler.Object);

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, loginMessage, CancellationToken.None);

        _mockLoginHandler.Verify(
            h => h.HandleAsync(_mockWebSocket.Object, It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithValidMessage_ShouldPassWebSocketContextToHandler()
    {
        var message = CreateMessage("UPDATE_RESOURCES", new { type = "Coins", value = 100 });
        var capturedWebSocket = default(WebSocket);

        _mockResourceHandler.Setup(h => h.HandleAsync(
            It.IsAny<WebSocket>(),
            It.IsAny<JsonElement>(),
            It.IsAny<CancellationToken>()))
            .Callback<WebSocket, JsonElement, CancellationToken>((ws, _, _) => capturedWebSocket = ws)
            .ReturnsAsync(Result.Success());

        _serviceProvider.RegisterKeyedService("UPDATE_RESOURCES", _mockResourceHandler.Object);

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, message, CancellationToken.None);

        Assert.Same(_mockWebSocket.Object, capturedWebSocket);
    }

    [Fact]
    public async Task DispatchAsync_WithUnknownMessageType_ShouldNotInvokeAnyHandler()
    {
        var unknownMessage = CreateMessage("UNKNOWN_TYPE", new { data = "test" });

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, unknownMessage, CancellationToken.None);

        _mockLoginHandler.Verify(
            h => h.HandleAsync(It.IsAny<WebSocket>(), It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockResourceHandler.Verify(
            h => h.HandleAsync(It.IsAny<WebSocket>(), It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WithMalformedJson_ShouldNotThrowException()
    {
        var malformedJson = Encoding.UTF8.GetBytes("{ invalid json }");

        var exception = await Record.ExceptionAsync(async () =>
            await _dispatcher.DispatchAsync(_mockWebSocket.Object, malformedJson, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task DispatchAsync_WithMalformedJson_ShouldNotInvokeAnyHandler()
    {
        var malformedJson = Encoding.UTF8.GetBytes("{ not valid json");
        _serviceProvider.RegisterKeyedService("LOGIN", _mockLoginHandler.Object);

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, malformedJson, CancellationToken.None);

        Assert.Empty(_serviceProvider.RequestedKeys);
    }

    [Fact]
    public async Task DispatchAsync_WithNullEnvelope_ShouldNotInvokeHandler()
    {
        var nullTypeMessage = Encoding.UTF8.GetBytes("null");
        _serviceProvider.RegisterKeyedService("LOGIN", _mockLoginHandler.Object);

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, nullTypeMessage, CancellationToken.None);

        Assert.Empty(_serviceProvider.RequestedKeys);
    }

    [Fact]
    public async Task DispatchAsync_WithEmptyType_ShouldNotInvokeHandler()
    {
        var emptyTypeMessage = CreateMessage("", new { data = "test" });
        _serviceProvider.RegisterKeyedService("", _mockLoginHandler.Object);

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, emptyTypeMessage, CancellationToken.None);

        Assert.Empty(_serviceProvider.RequestedKeys);
    }

    [Fact]
    public async Task DispatchAsync_WithMultipleMessages_ShouldRouteEachCorrectly()
    {
        var loginMessage = CreateMessage("LOGIN", new { deviceId = "device123" });
        var resourceMessage = CreateMessage("UPDATE_RESOURCES", new { type = "Coins", value = 100 });

        _mockLoginHandler.Setup(h => h.HandleAsync(
            It.IsAny<WebSocket>(),
            It.IsAny<JsonElement>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _mockResourceHandler.Setup(h => h.HandleAsync(
            It.IsAny<WebSocket>(),
            It.IsAny<JsonElement>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _serviceProvider.RegisterKeyedService("LOGIN", _mockLoginHandler.Object);
        _serviceProvider.RegisterKeyedService("UPDATE_RESOURCES", _mockResourceHandler.Object);

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, loginMessage, CancellationToken.None);
        await _dispatcher.DispatchAsync(_mockWebSocket.Object, resourceMessage, CancellationToken.None);

        _mockLoginHandler.Verify(
            h => h.HandleAsync(It.IsAny<WebSocket>(), It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockResourceHandler.Verify(
            h => h.HandleAsync(It.IsAny<WebSocket>(), It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithCaseInsensitiveType_ShouldRouteCorrectly()
    {
        var mixedCaseMessage = CreateMessage("login", new { deviceId = "device123" });

        _mockLoginHandler.Setup(h => h.HandleAsync(
            It.IsAny<WebSocket>(),
            It.IsAny<JsonElement>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _serviceProvider.RegisterKeyedService("login", _mockLoginHandler.Object);

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, mixedCaseMessage, CancellationToken.None);

        _mockLoginHandler.Verify(
            h => h.HandleAsync(It.IsAny<WebSocket>(), It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithCancellationToken_ShouldPassToHandler()
    {
        var message = CreateMessage("LOGIN", new { deviceId = "device123" });
        var cts = new CancellationTokenSource();
        var capturedToken = CancellationToken.None;

        _mockLoginHandler.Setup(h => h.HandleAsync(
            It.IsAny<WebSocket>(),
            It.IsAny<JsonElement>(),
            It.IsAny<CancellationToken>()))
            .Callback<WebSocket, JsonElement, CancellationToken>((_, _, ct) => capturedToken = ct)
            .ReturnsAsync(Result.Success());

        _serviceProvider.RegisterKeyedService("LOGIN", _mockLoginHandler.Object);

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, message, cts.Token);

        Assert.Equal(cts.Token, capturedToken);
    }

    [Fact]
    public async Task DispatchAsync_WithPayloadData_ShouldPassPayloadToHandler()
    {
        var expectedDeviceId = "device123";
        var message = CreateMessage("LOGIN", new { deviceId = expectedDeviceId });
        JsonElement capturedPayload = default;

        _mockLoginHandler.Setup(h => h.HandleAsync(
            It.IsAny<WebSocket>(),
            It.IsAny<JsonElement>(),
            It.IsAny<CancellationToken>()))
            .Callback<WebSocket, JsonElement, CancellationToken>((_, payload, _) => capturedPayload = payload)
            .ReturnsAsync(Result.Success());

        _serviceProvider.RegisterKeyedService("LOGIN", _mockLoginHandler.Object);

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, message, CancellationToken.None);

        var actualDeviceId = capturedPayload.GetProperty("deviceId").GetString();
        Assert.Equal(expectedDeviceId, actualDeviceId);
    }

    private static ReadOnlyMemory<byte> CreateMessage(string type, object payload)
    {
        var envelope = new MessageEnvelope
        {
            Type = type,
            Payload = JsonSerializer.SerializeToElement(payload)
        };

        var json = JsonSerializer.Serialize(envelope);
        return Encoding.UTF8.GetBytes(json);
    }

    private sealed class TestServiceProvider : IServiceProvider, IKeyedServiceProvider
    {
        private readonly Dictionary<string, object> _keyedServices = new();
        public List<string> RequestedKeys { get; } = new();

        public void RegisterKeyedService(string key, object service)
        {
            _keyedServices[key] = service;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IKeyedServiceProvider))
                return this;

            return null;
        }

        public object? GetKeyedService(Type serviceType, object? serviceKey)
        {
            if (serviceKey is string key)
            {
                RequestedKeys.Add(key);
                return _keyedServices.TryGetValue(key, out var service) ? service : null;
            }

            return null;
        }

        public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
        {
            return GetKeyedService(serviceType, serviceKey)
                ?? throw new InvalidOperationException($"Service {serviceType} with key {serviceKey} not found");
        }
    }
}
