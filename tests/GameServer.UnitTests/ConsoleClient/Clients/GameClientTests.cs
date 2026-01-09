using GameServer.ConsoleClient.Clients;
using GameServer.ConsoleClient.Model.Request;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.UnitTests.ConsoleClient.Clients;

public class GameClientTests : IAsyncDisposable
{
    private GameClient? _client;

    [Fact]
    public void Constructor_WithDefaultParameters_ShouldCreateClient()
    {
        _client = new GameClient(NullLogger<GameClient>.Instance);

        Assert.NotNull(_client);
        Assert.Equal(WebSocketState.None, _client.State);
        Assert.False(_client.IsConnected);
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        _client = new GameClient(NullLogger<GameClient>.Instance);

        await _client.DisposeAsync();
        await _client.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => _client.GetUnderlyingSocket());
    }

    [Fact]
    public async Task SendAsync_WhenNotConnected_ShouldThrowInvalidOperationException()
    {
        _client = new GameClient(NullLogger<GameClient>.Instance);
        var request = new LoginRequest("test-device");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.SendAsync(request));

        Assert.Equal("WebSocket is not connected.", exception.Message);
    }

    [Fact]
    public async Task SendAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        _client = new GameClient(NullLogger<GameClient>.Instance);
        await _client.DisposeAsync();
        var request = new LoginRequest("test-device");

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _client.SendAsync(request));
    }

    [Fact]
    public void StartListening_WhenNotConnected_ShouldThrowInvalidOperationException()
    {
        _client = new GameClient(NullLogger<GameClient>.Instance);

        var exception = Assert.Throws<InvalidOperationException>(() => _client.StartListening());

        Assert.Equal("WebSocket is not connected.", exception.Message);
    }

    [Fact]
    public void MessageTypes_ShouldBeCorrectlyDefined()
    {
        Assert.Equal("LOGIN", LoginRequest.MessageType);
        Assert.Equal("UPDATE_RESOURCES", UpdateResourceRequest.MessageType);
        Assert.Equal("SEND_GIFT", SendGiftRequest.MessageType);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            try
            {
                await _client.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
