using System.Text;
using System.Text.Json;
using GameServer.Application.Services;
using GameServer.Application.Common.Interfaces;
using GameServer.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.UnitTests.Application.Services;

public class MessageDispatcherTests
{
    private readonly TestServiceProvider _serviceProvider;
    private readonly Mock<IMessageHandler> _mockLoginHandler;
    private readonly Mock<WebSocket> _mockWebSocket;
    private readonly MessageDispatcher _dispatcher;

    public MessageDispatcherTests()
    {
        _serviceProvider = new TestServiceProvider();
        _mockLoginHandler = new Mock<IMessageHandler>();
        _mockWebSocket = new Mock<WebSocket>();
        _dispatcher = new MessageDispatcher(_serviceProvider);
    }

    [Fact]
    public async Task DispatchAsync_WithValidLoginMessage_ShouldInvokeCorrectHandler()
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
    public async Task DispatchAsync_WithUnknownMessageType_ShouldNotInvokeHandler()
    {
        var unknownMessage = CreateMessage("UNKNOWN_TYPE", new { data = "test" });

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, unknownMessage, CancellationToken.None);

        _mockLoginHandler.Verify(
            h => h.HandleAsync(It.IsAny<WebSocket>(), It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WithMalformedJson_ShouldNotThrow()
    {
        var malformedJson = Encoding.UTF8.GetBytes("{ invalid json }");

        var exception = await Record.ExceptionAsync(async () =>
            await _dispatcher.DispatchAsync(_mockWebSocket.Object, malformedJson, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task DispatchAsync_ShouldPassWebSocketContextToHandler()
    {
        var message = CreateMessage("LOGIN", new { deviceId = "device123" });
        WebSocket? capturedWebSocket = null;

        _mockLoginHandler.Setup(h => h.HandleAsync(
            It.IsAny<WebSocket>(),
            It.IsAny<JsonElement>(),
            It.IsAny<CancellationToken>()))
            .Callback<WebSocket, JsonElement, CancellationToken>((ws, _, _) => capturedWebSocket = ws)
            .ReturnsAsync(Result.Success());

        _serviceProvider.RegisterKeyedService("LOGIN", _mockLoginHandler.Object);

        await _dispatcher.DispatchAsync(_mockWebSocket.Object, message, CancellationToken.None);

        Assert.Same(_mockWebSocket.Object, capturedWebSocket);
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

        public void RegisterKeyedService(string key, object service) => _keyedServices[key] = service;

        public object? GetService(Type serviceType) => 
            serviceType == typeof(IKeyedServiceProvider) ? this : null;

        public object? GetKeyedService(Type serviceType, object? serviceKey) =>
            serviceKey is string key && _keyedServices.TryGetValue(key, out var service) ? service : null;

        public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
            GetKeyedService(serviceType, serviceKey) ?? throw new InvalidOperationException();
    }
}
