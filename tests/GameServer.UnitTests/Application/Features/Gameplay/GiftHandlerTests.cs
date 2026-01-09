using System.Net.WebSockets;
using System.Text.Json;
using Moq;
using GameServer.Application.Features.Gameplay;
using GameServer.Domain.Common;
using GameServer.Domain.Enums;
using GameServer.Domain.Interfaces;

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
            _mockGameNotifier.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldDerivePlayerIdFromSocket_NotPayload()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);
        WebSocket? capturedSocket = null;

        _mockSessionManager
            .Setup(s => s.GetPlayerId(It.IsAny<WebSocket>()))
            .Callback<WebSocket>(ws => capturedSocket = ws)
            .Returns(senderId);
        _mockSessionManager.Setup(s => s.IsPlayerOnline(friendId)).Returns(false);
        
        _mockRepository
            .Setup(r => r.GetFriendIdsAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Guid>>.Success(new[] { friendId }));
        _mockRepository
            .Setup(r => r.GetResourceAmountAsync(senderId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(1000));
        _mockRepository
            .Setup(r => r.GetResourceAmountAsync(friendId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(500));
        _mockRepository
            .Setup(r => r.UpdateResourceAsync(It.IsAny<Guid>(), ResourceType.Coins, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mockRepository
            .Setup(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<Result>>, CancellationToken>(async (action, _) => await action());

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.Same(_mockWebSocket.Object, capturedSocket);
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
        _mockRepository.Verify(r => r.GetFriendIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _mockRepository.Verify(r => r.GetFriendIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task HandleAsync_WhenAmountIsZeroOrNegative_ShouldReturnInvalidAmountError(long invalidValue)
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, invalidValue);

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InvalidAmount", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidResourceType_ShouldReturnError()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, (ResourceType)999, 100);

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InvalidResourceType", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenPlayersAreNotFriends_ShouldReturnNotFriendsError()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var nonFriendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);
        _mockRepository
            .Setup(r => r.GetFriendIdsAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Guid>>.Success(new[] { nonFriendId }));

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFriends", result.Error?.Code);
        _mockSynchronizationProvider.Verify(
            s => s.AcquireLocksAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), 
            Times.Never);
        _mockRepository.Verify(
            r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenPlayersAreNotFriends_ShouldNotStartTransaction()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);
        _mockRepository
            .Setup(r => r.GetFriendIdsAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Guid>>.Success(Array.Empty<Guid>()));

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        _mockRepository.Verify(
            r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenInsufficientFunds_ShouldReturnError()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var senderBalance = 50L;
        var giftAmount = 100L;
        var payload = CreatePayload(friendId, ResourceType.Coins, giftAmount);

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);
        _mockRepository
            .Setup(r => r.GetFriendIdsAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Guid>>.Success(new[] { friendId }));
        _mockRepository
            .Setup(r => r.GetResourceAmountAsync(senderId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(senderBalance));

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("InsufficientFunds", result.Error?.Code);
        _mockRepository.Verify(
            r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenInsufficientFunds_ShouldNotStartTransaction()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 1000);

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);
        _mockRepository
            .Setup(r => r.GetFriendIdsAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Guid>>.Success(new[] { friendId }));
        _mockRepository
            .Setup(r => r.GetResourceAmountAsync(senderId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(100));

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        _mockRepository.Verify(
            r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithSuccessfulGift_ShouldDeductFromSenderAndAddToFriend()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var senderBalance = 1000L;
        var friendBalance = 500L;
        var giftAmount = 100L;
        var payload = CreatePayload(friendId, ResourceType.Coins, giftAmount);

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, senderBalance, friendBalance, giftAmount);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(
            r => r.UpdateResourceAsync(senderId, ResourceType.Coins, senderBalance - giftAmount, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRepository.Verify(
            r => r.UpdateResourceAsync(friendId, ResourceType.Coins, friendBalance + giftAmount, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithSuccessfulGift_ShouldExecuteInTransaction()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, 1000, 500, 100);

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        _mockRepository.Verify(
            r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldAcquireLocksForBothPlayerIds()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);
        Guid? capturedId1 = null;
        Guid? capturedId2 = null;

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);
        _mockSessionManager.Setup(s => s.IsPlayerOnline(friendId)).Returns(false);
        _mockRepository
            .Setup(r => r.GetFriendIdsAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Guid>>.Success(new[] { friendId }));
        _mockRepository
            .Setup(r => r.GetResourceAmountAsync(senderId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(1000));
        _mockRepository
            .Setup(r => r.GetResourceAmountAsync(friendId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(500));
        _mockRepository
            .Setup(r => r.UpdateResourceAsync(It.IsAny<Guid>(), ResourceType.Coins, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mockRepository
            .Setup(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<Result>>, CancellationToken>(async (action, _) => await action());
        _mockSynchronizationProvider
            .Setup(s => s.AcquireLocksAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, CancellationToken>((id1, id2, _) => { capturedId1 = id1; capturedId2 = id2; })
            .ReturnsAsync(_mockLockHandle.Object);

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.Equal(senderId, capturedId1);
        Assert.Equal(friendId, capturedId2);
        _mockSynchronizationProvider.Verify(
            s => s.AcquireLocksAsync(senderId, friendId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldReleaseLockAfterCompletion()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, 1000, 500, 100);

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        _mockLockHandle.Verify(l => l.Dispose(), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenFriendIsOnline_ShouldSendNotification()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var giftAmount = 100L;
        var payload = CreatePayload(friendId, ResourceType.Coins, giftAmount);

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, 1000, 500, giftAmount);
        _mockSessionManager.Setup(s => s.IsPlayerOnline(friendId)).Returns(true);

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        _mockGameNotifier.Verify(
            n => n.SendToPlayerAsync(friendId, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Once);
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

        _mockGameNotifier.Verify(
            n => n.SendToPlayerAsync(It.IsAny<Guid>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTransactionFails_ShouldReturnError()
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
            .ReturnsAsync(Result<long>.Success(1000));
        _mockRepository
            .Setup(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(new Error("TransactionFailed", "Database error")));

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TransactionFailed", result.Error?.Code);
        _mockGameNotifier.Verify(
            n => n.SendToPlayerAsync(It.IsAny<Guid>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTransactionFails_ShouldNotNotifyFriend()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);
        _mockSessionManager.Setup(s => s.IsPlayerOnline(friendId)).Returns(true);
        _mockRepository
            .Setup(r => r.GetFriendIdsAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Guid>>.Success(new[] { friendId }));
        _mockRepository
            .Setup(r => r.GetResourceAmountAsync(senderId, ResourceType.Coins, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(1000));
        _mockRepository
            .Setup(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(new Error("TransactionFailed", "Database error")));

        await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        _mockGameNotifier.Verify(
            n => n.SendToPlayerAsync(It.IsAny<Guid>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithMalformedPayload_ShouldReturnInvalidPayloadError()
    {
        var senderId = Guid.NewGuid();
        var malformedPayload = JsonDocument.Parse("{ \"invalid\": 123 }").RootElement;

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, malformedPayload, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(ResourceType.Coins)]
    [InlineData(ResourceType.Rolls)]
    public async Task HandleAsync_WithValidResourceTypes_ShouldSucceed(ResourceType resourceType)
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, resourceType, 100);

        SetupSuccessfulGiftScenario(senderId, friendId, resourceType, 1000, 500, 100);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_ShouldPassCancellationTokenToAllOperations()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);
        var cts = new CancellationTokenSource();

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, 1000, 500, 100);

        await _handler.HandleAsync(_mockWebSocket.Object, payload, cts.Token);

        _mockRepository.Verify(
            r => r.GetFriendIdsAsync(senderId, cts.Token),
            Times.Once);
        _mockSynchronizationProvider.Verify(
            s => s.AcquireLocksAsync(senderId, friendId, cts.Token),
            Times.Once);
        _mockRepository.Verify(
            r => r.GetResourceAmountAsync(senderId, ResourceType.Coins, cts.Token),
            Times.Once);
        _mockRepository.Verify(
            r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithExactBalance_ShouldSucceedWithZeroRemainingBalance()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var senderBalance = 100L;
        var giftAmount = 100L;
        var payload = CreatePayload(friendId, ResourceType.Coins, giftAmount);

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, senderBalance, 500, giftAmount);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(
            r => r.UpdateResourceAsync(senderId, ResourceType.Coins, 0, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenGetFriendIdsFails_ShouldReturnError()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var payload = CreatePayload(friendId, ResourceType.Coins, 100);

        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);
        _mockRepository
            .Setup(r => r.GetFriendIdsAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Guid>>.Failure(new Error("DatabaseError", "Connection failed")));

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("DatabaseError", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenGetSenderBalanceFails_ShouldReturnError()
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
            .ReturnsAsync(Result<long>.Failure(new Error("DatabaseError", "Connection failed")));

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("DatabaseError", result.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_WithLargeGiftAmount_ShouldSucceed()
    {
        var senderId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var largeAmount = 999_999_999L;
        var payload = CreatePayload(friendId, ResourceType.Coins, largeAmount);

        SetupSuccessfulGiftScenario(senderId, friendId, ResourceType.Coins, largeAmount, 0, largeAmount);

        var result = await _handler.HandleAsync(_mockWebSocket.Object, payload, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private void SetupSuccessfulGiftScenario(
        Guid senderId, 
        Guid friendId, 
        ResourceType resourceType, 
        long senderBalance, 
        long friendBalance, 
        long giftAmount)
    {
        _mockSessionManager.Setup(s => s.GetPlayerId(It.IsAny<WebSocket>())).Returns(senderId);
        _mockSessionManager.Setup(s => s.IsPlayerOnline(friendId)).Returns(false);
        
        _mockRepository
            .Setup(r => r.GetFriendIdsAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Guid>>.Success(new[] { friendId }));
        
        _mockRepository
            .Setup(r => r.GetResourceAmountAsync(senderId, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(senderBalance));
        
        _mockRepository
            .Setup(r => r.GetResourceAmountAsync(friendId, resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long>.Success(friendBalance));
        
        _mockRepository
            .Setup(r => r.UpdateResourceAsync(senderId, resourceType, senderBalance - giftAmount, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        
        _mockRepository
            .Setup(r => r.UpdateResourceAsync(friendId, resourceType, friendBalance + giftAmount, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        
        _mockRepository
            .Setup(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<Result>>, CancellationToken>(async (action, _) => await action());
    }

    private static JsonElement CreatePayload(Guid friendPlayerId, ResourceType type, long value)
    {
        var json = $$"""{"FriendPlayerId":"{{friendPlayerId}}","Type":{{(int)type}},"Value":{{value}}}""";
        return JsonDocument.Parse(json).RootElement;
    }
}

