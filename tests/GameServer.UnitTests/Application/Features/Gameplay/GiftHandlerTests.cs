using System.Text.Json;
using GameServer.Application.Features.Gameplay;
using GameServer.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.UnitTests.Application.Features.Gameplay;

public class GiftHandlerTests
{
    private readonly Mock<IStateRepository> _mockRepository;
    private readonly Mock<ISessionManager> _mockSessionManager;
    private readonly Mock<ISynchronizationProvider> _mockSynchronizationProvider;
    private readonly Mock<IGameNotifier> _mockGameNotifier;
    private readonly Mock<WebSocket> _mockWebSocket;
    private readonly Mock<IDisposable> _mockLockHandle;
    private readonly GiftHandler _handler;

    public GiftHandlerTests()
    {
        _mockRepository = new Mock<IStateRepository>();
        _mockSessionManager = new Mock<ISessionManager>();
        _mockSynchronizationProvider = new Mock<ISynchronizationProvider>();
        _mockGameNotifier = new Mock<IGameNotifier>();
        _mockWebSocket = new Mock<WebSocket>();
        _mockLockHandle = new Mock<IDisposable>();
        
        _mockSynchronizationProvider
            .Setup(s => s.AcquireLocksAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockLockHandle.Object);
        
        _handler = new GiftHandler(
            _mockRepository.Object,
            _mockSessionManager.Object,
            _mockSynchronizationProvider.Object,
            _mockGameNotifier.Object,
            NullLogger<GiftHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ShouldDerivePlayerIdFromSocket()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, 1000, 500, 100);

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        _mockSessionManager.Verify(s => s.GetPlayerId(_mockWebSocket.Object), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenSocketNotRegistered_ShouldReturnUnauthorizedError()
    {
        var payload = CreatePayload(Guid.NewGuid(), ResourceType.Coins, 100);
        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns((Guid?)null);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unauthorized", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenSendingGiftToSelf_ShouldReturnInvalidRecipientError()
    {
        var playerId = Guid.NewGuid();
        var payload = CreatePayload(playerId, ResourceType.Coins, 100);
        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(playerId);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InvalidRecipient", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenPlayersAreNotFriends_ShouldReturnNotFriendsError()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);
        _mockRepository
            .Setup(r => r.GetFriendIdsAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Guid>>.Success(Array.Empty<Guid>()));

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFriends", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenInsufficientFunds_ShouldReturnError()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);
        _mockRepository
            .Setup(r => r.GetFriendIdsAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Guid>>.Success(new[] { friendId }));
        _mockRepository
            .Setup(r => r.GetResourceAmountAsync(senderId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(50));

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InsufficientFunds", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_WithSuccessfulGift_ShouldDeductFromSenderAndAddToFriend()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, 1000, 500, 100);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(r => r.UpdateResourceAsync(senderId, ResourceType.Coins, 900, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateResourceAsync(friendId, ResourceType.Coins, 600, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldAcquireLocksForBothPlayers()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, 1000, 500, 100);

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        _mockSynchronizationProvider.Verify(s => s.AcquireLocksAsync(senderId, friendId, It.IsAny<CancellationToken>()), Times.Once);
        _mockLockHandle.Verify(l => l.Dispose(), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenFriendIsOnline_ShouldSendNotification()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, 1000, 500, 100);
        _mockSessionManager.Setup(s => s.IsPlayerOnline(friendId)).Returns(true);

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        _mockGameNotifier.Verify(n => n.SendToPlayerAsync(friendId, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenFriendIsOffline_ShouldNotSendNotification()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, 1000, 500, 100);
        _mockSessionManager.Setup(s => s.IsPlayerOnline(friendId)).Returns(false);

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        _mockGameNotifier.Verify(n => n.SendToPlayerAsync(It.IsAny<Guid>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldExecuteInTransaction()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, 1000, 500, 100);

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        _mockRepository.Verify(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupSuccessfulGiftScenario(Guid senderId, Guid friendId, ResourceType resourceType, long senderBalance, long friendBalance, long giftAmount)
    {
        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);
        _mockSessionManager.Setup(s => s.IsPlayerOnline(friendId)).Returns(false);
        _mockRepository.Setup(r => r.GetFriendIdsAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Guid>>.Success(new[] { friendId }));
        _mockRepository.Setup(r => r.GetResourceAmountAsync(senderId, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(senderBalance));
        _mockRepository.Setup(r => r.GetResourceAmountAsync(friendId, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(friendBalance));
        _mockRepository.Setup(r => r.UpdateResourceAsync(senderId, resourceType, senderBalance - giftAmount, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mockRepository.Setup(r => r.UpdateResourceAsync(friendId, resourceType, friendBalance + giftAmount, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mockRepository.Setup(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<Result>>, CancellationToken>(async (action, _) => await action());
    }

    private static JsonElement CreatePayload(Guid friendPlayerId, ResourceType type, long value)
    {
        var json = $$"""{"FriendPlayerId":"{{friendPlayerId}}","Type":{{(int)type}},"Value":{{value}}}""";
        return JsonDocument.Parse(json).RootElement;
    }
}
