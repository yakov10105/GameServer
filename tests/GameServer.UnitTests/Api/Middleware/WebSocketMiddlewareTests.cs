using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GameServer.Api.Configuration;
using GameServer.Api.Middleware;
using GameServer.Domain.Interfaces;

namespace GameServer.UnitTests.Api.Middleware;

public class WebSocketMiddlewareTests
{
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<ILogger<WebSocketMiddleware>> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<ISessionManager> _mockSessionManager;
    private readonly WebSocketMiddleware _middleware;

    public WebSocketMiddlewareTests()
    {
        _mockNext = new Mock<RequestDelegate>();
        _mockLogger = new Mock<ILogger<WebSocketMiddleware>>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockSessionManager = new Mock<ISessionManager>();
        
        var options = Options.Create(new GameServerOptions());
        
        _middleware = new WebSocketMiddleware(
            _mockNext.Object,
            _mockLogger.Object,
            _mockScopeFactory.Object,
            _mockSessionManager.Object,
            options);
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
