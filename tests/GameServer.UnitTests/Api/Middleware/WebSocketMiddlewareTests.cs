using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using GameServer.Api.Middleware;
using GameServer.Application.Common.Interfaces;
using System.Net.WebSockets;

namespace GameServer.UnitTests.Api.Middleware;

public class WebSocketMiddlewareTests
{
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<ILogger<WebSocketMiddleware>> _mockLogger;
    private readonly Mock<IMessageDispatcher> _mockDispatcher;
    private readonly WebSocketMiddleware _middleware;

    public WebSocketMiddlewareTests()
    {
        _mockNext = new Mock<RequestDelegate>();
        _mockLogger = new Mock<ILogger<WebSocketMiddleware>>();
        _mockDispatcher = new Mock<IMessageDispatcher>();
        _middleware = new WebSocketMiddleware(_mockNext.Object, _mockLogger.Object, _mockDispatcher.Object);
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestIsNotWebSocket_ShouldCallNext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/health";

        await _middleware.InvokeAsync(httpContext);

        _mockNext.Verify(n => n(httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestIsNotWebSocket_ShouldNotIncrementActiveConnections()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        var initialActiveConnections = WebSocketMiddleware.ActiveConnections;

        await _middleware.InvokeAsync(httpContext);

        Assert.Equal(initialActiveConnections, WebSocketMiddleware.ActiveConnections);
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestIsNotWebSocket_ShouldNotCallDispatcher()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";

        await _middleware.InvokeAsync(httpContext);

        _mockDispatcher.Verify(
            d => d.DispatchAsync(It.IsAny<WebSocket>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestIsNotWebSocket_ShouldNotLogConnectionAccepted()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        await _middleware.InvokeAsync(httpContext);

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Connection accepted")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}

