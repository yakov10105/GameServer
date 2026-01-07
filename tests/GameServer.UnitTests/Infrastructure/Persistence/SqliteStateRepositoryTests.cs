using GameServer.Infrastructure.Persistence.Context;
using GameServer.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;

namespace GameServer.UnitTests.Infrastructure.Persistence;

public sealed class SqliteStateRepositoryTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly SqliteStateRepository _repository;
    private readonly SqliteConnection _connection;

    public SqliteStateRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new GameDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new SqliteStateRepository(_context);
    }

    [Fact]
    public async Task CreatePlayerAsync_WithNewDeviceId_ShouldPersistToDatabase()
    {
        var deviceId = "device123";

        var result = await _repository.CreatePlayerAsync(deviceId);

        Assert.True(result.IsSuccess);
        var playerId = result.Value;
        var retrievedPlayer = await _context.Players
            .Include(p => p.Resources)
            .FirstOrDefaultAsync(p => p.Id == playerId);
        Assert.NotNull(retrievedPlayer);
        Assert.Equal(deviceId, retrievedPlayer.DeviceId);
        Assert.Equal(2, retrievedPlayer.Resources.Count);
    }

    [Fact]
    public async Task CreatePlayerAsync_WithNewDeviceId_ShouldCreateDefaultResources()
    {
        var deviceId = "device456";

        var result = await _repository.CreatePlayerAsync(deviceId);

        Assert.True(result.IsSuccess);
        var playerId = result.Value;
        var resources = await _context.Resources
            .Where(r => r.PlayerId == playerId)
            .ToListAsync();
        Assert.Equal(2, resources.Count);
        Assert.Contains(resources, r => r.Type == ResourceType.Coins && r.Amount == 0);
        Assert.Contains(resources, r => r.Type == ResourceType.Rolls && r.Amount == 0);
    }

    [Fact]
    public async Task CreatePlayerAsync_WithExistingDeviceId_ShouldReturnExistingPlayerId()
    {
        var deviceId = "device789";
        var firstResult = await _repository.CreatePlayerAsync(deviceId);
        var firstPlayerId = firstResult.Value;

        var secondResult = await _repository.CreatePlayerAsync(deviceId);

        Assert.True(secondResult.IsSuccess);
        Assert.Equal(firstPlayerId, secondResult.Value);
        var playerCount = await _context.Players.CountAsync(p => p.DeviceId == deviceId);
        Assert.Equal(1, playerCount);
    }

    [Fact]
    public async Task GetPlayerIdByDeviceIdAsync_WhenPlayerExists_ShouldReturnPlayerId()
    {
        var deviceId = "device_existing";
        var createResult = await _repository.CreatePlayerAsync(deviceId);
        var expectedPlayerId = createResult.Value;

        var result = await _repository.GetPlayerIdByDeviceIdAsync(deviceId);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedPlayerId, result.Value);
    }

    [Fact]
    public async Task GetPlayerIdByDeviceIdAsync_WhenPlayerDoesNotExist_ShouldReturnFailure()
    {
        var nonExistentDeviceId = "nonexistent_device";

        var result = await _repository.GetPlayerIdByDeviceIdAsync(nonExistentDeviceId);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Player.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetResourceAmountAsync_WhenResourceExists_ShouldReturnAmount()
    {
        var deviceId = "device_resource";
        var createResult = await _repository.CreatePlayerAsync(deviceId);
        var playerId = createResult.Value;
        await _repository.UpdateResourceAsync(playerId, ResourceType.Coins, 100);

        var result = await _repository.GetResourceAmountAsync(playerId, ResourceType.Coins);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public async Task GetResourceAmountAsync_WhenResourceDoesNotExist_ShouldReturnFailure()
    {
        var nonExistentPlayerId = Guid.NewGuid();

        var result = await _repository.GetResourceAmountAsync(nonExistentPlayerId, ResourceType.Coins);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Resource.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateResourceAsync_WhenResourceExists_ShouldUpdateAmount()
    {
        var deviceId = "device_update";
        var createResult = await _repository.CreatePlayerAsync(deviceId);
        var playerId = createResult.Value;
        var newAmount = 500L;

        var result = await _repository.UpdateResourceAsync(playerId, ResourceType.Coins, newAmount);

        Assert.True(result.IsSuccess);
        var resource = await _context.Resources
            .FirstOrDefaultAsync(r => r.PlayerId == playerId && r.Type == ResourceType.Coins);
        Assert.NotNull(resource);
        Assert.Equal(newAmount, resource.Amount);
    }

    [Fact]
    public async Task UpdateResourceAsync_WhenResourceDoesNotExist_ShouldReturnFailure()
    {
        var nonExistentPlayerId = Guid.NewGuid();

        var result = await _repository.UpdateResourceAsync(nonExistentPlayerId, ResourceType.Coins, 100);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Resource.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddFriendshipAsync_ShouldEnforceNormalizedConstraint()
    {
        var player1Result = await _repository.CreatePlayerAsync("device1");
        var player2Result = await _repository.CreatePlayerAsync("device2");
        var player1Id = player1Result.Value;
        var player2Id = player2Result.Value;

        await _repository.AddFriendshipAsync(player2Id, player1Id);

        var friendship = await _context.Friendships.FirstOrDefaultAsync();
        Assert.NotNull(friendship);
        Assert.True(friendship.PlayerId1 < friendship.PlayerId2);
        var expectedSmallerId = player1Id < player2Id ? player1Id : player2Id;
        var expectedLargerId = player1Id > player2Id ? player1Id : player2Id;
        Assert.Equal(expectedSmallerId, friendship.PlayerId1);
        Assert.Equal(expectedLargerId, friendship.PlayerId2);
    }

    [Fact]
    public async Task AddFriendshipAsync_WithSamePlayerIds_ShouldEnforceNormalization()
    {
        var player1Result = await _repository.CreatePlayerAsync("deviceA");
        var player2Result = await _repository.CreatePlayerAsync("deviceB");
        var player1Id = player1Result.Value;
        var player2Id = player2Result.Value;

        await _repository.AddFriendshipAsync(player1Id, player2Id);

        var friendship = await _context.Friendships.FirstOrDefaultAsync();
        Assert.NotNull(friendship);
        Assert.Equal(player1Id < player2Id ? player1Id : player2Id, friendship.PlayerId1);
        Assert.Equal(player1Id > player2Id ? player1Id : player2Id, friendship.PlayerId2);
    }

    [Fact]
    public async Task AddFriendshipAsync_WhenPlayerDoesNotExist_ShouldReturnFailure()
    {
        var player1Result = await _repository.CreatePlayerAsync("device_exists");
        var player1Id = player1Result.Value;
        var nonExistentPlayerId = Guid.NewGuid();

        var result = await _repository.AddFriendshipAsync(player1Id, nonExistentPlayerId);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Player.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddFriendshipAsync_WhenFriendshipAlreadyExists_ShouldReturnFailure()
    {
        var player1Result = await _repository.CreatePlayerAsync("device_friend1");
        var player2Result = await _repository.CreatePlayerAsync("device_friend2");
        var player1Id = player1Result.Value;
        var player2Id = player2Result.Value;
        await _repository.AddFriendshipAsync(player1Id, player2Id);

        var result = await _repository.AddFriendshipAsync(player1Id, player2Id);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Friendship.AlreadyExists", result.Error.Code);
    }

    [Fact]
    public async Task GetFriendIdsAsync_ShouldReturnBidirectionalFriends()
    {
        var player1Result = await _repository.CreatePlayerAsync("device_bi1");
        var player2Result = await _repository.CreatePlayerAsync("device_bi2");
        var player3Result = await _repository.CreatePlayerAsync("device_bi3");
        var player1Id = player1Result.Value;
        var player2Id = player2Result.Value;
        var player3Id = player3Result.Value;
        await _repository.AddFriendshipAsync(player1Id, player2Id);
        await _repository.AddFriendshipAsync(player1Id, player3Id);

        var result = await _repository.GetFriendIdsAsync(player1Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(player2Id, result.Value);
        Assert.Contains(player3Id, result.Value);
    }

    [Fact]
    public async Task GetFriendIdsAsync_ForPlayerWithNoFriends_ShouldReturnEmptyList()
    {
        var playerResult = await _repository.CreatePlayerAsync("device_lonely");
        var playerId = playerResult.Value;

        var result = await _repository.GetFriendIdsAsync(playerId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetFriendIdsAsync_ShouldReturnFriendsRegardlessOfStorageOrder()
    {
        var player1Result = await _repository.CreatePlayerAsync("device_order1");
        var player2Result = await _repository.CreatePlayerAsync("device_order2");
        var player1Id = player1Result.Value;
        var player2Id = player2Result.Value;
        await _repository.AddFriendshipAsync(player2Id, player1Id);

        var result = await _repository.GetFriendIdsAsync(player2Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value);
        Assert.Contains(player1Id, result.Value);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenActionSucceeds_ShouldCommitChanges()
    {
        var player1Result = await _repository.CreatePlayerAsync("device_txn1");
        var player2Result = await _repository.CreatePlayerAsync("device_txn2");
        var player1Id = player1Result.Value;
        var player2Id = player2Result.Value;
        await _repository.UpdateResourceAsync(player1Id, ResourceType.Coins, 100);
        await _repository.UpdateResourceAsync(player2Id, ResourceType.Coins, 50);

        var result = await _repository.ExecuteInTransactionAsync(async () =>
        {
            await _repository.UpdateResourceAsync(player1Id, ResourceType.Coins, 80);
            await _repository.UpdateResourceAsync(player2Id, ResourceType.Coins, 70);
            return Result.Success();
        });

        Assert.True(result.IsSuccess);
        var player1Coins = await _repository.GetResourceAmountAsync(player1Id, ResourceType.Coins);
        var player2Coins = await _repository.GetResourceAmountAsync(player2Id, ResourceType.Coins);
        Assert.Equal(80, player1Coins.Value);
        Assert.Equal(70, player2Coins.Value);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenActionFails_ShouldRollbackChanges()
    {
        var playerResult = await _repository.CreatePlayerAsync("device_txn_rollback");
        var playerId = playerResult.Value;
        await _repository.UpdateResourceAsync(playerId, ResourceType.Coins, 100);

        var result = await _repository.ExecuteInTransactionAsync(async () =>
        {
            await _repository.UpdateResourceAsync(playerId, ResourceType.Coins, 50);
            return Result.Failure(new Error("Test.Error", "Simulated failure"));
        });

        Assert.False(result.IsSuccess);
        var coins = await _repository.GetResourceAmountAsync(playerId, ResourceType.Coins);
        Assert.Equal(100, coins.Value);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenExceptionThrown_ShouldRollbackChanges()
    {
        var playerResult = await _repository.CreatePlayerAsync("device_txn_exception");
        var playerId = playerResult.Value;
        await _repository.UpdateResourceAsync(playerId, ResourceType.Coins, 100);

        var result = await _repository.ExecuteInTransactionAsync(async () =>
        {
            await _repository.UpdateResourceAsync(playerId, ResourceType.Coins, 50);
            throw new InvalidOperationException("Simulated exception");
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("Transaction.Failed", result.Error!.Code);
        var coins = await _repository.GetResourceAmountAsync(playerId, ResourceType.Coins);
        Assert.Equal(100, coins.Value);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}

