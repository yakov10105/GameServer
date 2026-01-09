using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using GameServer.Api.Middleware;
using GameServer.Application.Common.Interfaces;

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
}
