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
    public async Task CreatePlayerAsync_ShouldPersistWithDefaultResources()
    {
        var deviceId = "device123";

        var result = await _repository.CreatePlayerAsync(deviceId);

        Assert.True(result.IsSuccess);
        var retrievedPlayer = await _context.Players
            .Include(p => p.Resources)
            .FirstOrDefaultAsync(p => p.Id == result.Value);
        Assert.NotNull(retrievedPlayer);
        Assert.Equal(deviceId, retrievedPlayer.DeviceId);
        Assert.Equal(2, retrievedPlayer.Resources.Count);
    }

    [Fact]
    public async Task GetPlayerIdByDeviceIdAsync_WhenPlayerDoesNotExist_ShouldReturnFailure()
    {
        var result = await _repository.GetPlayerIdByDeviceIdAsync("nonexistent");

        Assert.False(result.IsSuccess);
        Assert.Equal("Player.NotFound", result.Error?.Code);
    }

    [Fact]
    public async Task UpdateResourceAsync_ShouldUpdateAmount()
    {
        var createResult = await _repository.CreatePlayerAsync("device");
        var playerId = createResult.Value;

        await _repository.UpdateResourceAsync(playerId, ResourceType.Coins, 500);

        var resource = await _context.Resources.FirstOrDefaultAsync(r => r.PlayerId == playerId && r.Type == ResourceType.Coins);
        Assert.Equal(500, resource?.Amount);
    }

    [Fact]
    public async Task AddFriendshipAsync_ShouldNormalizePlayerIds()
    {
        var player1Result = await _repository.CreatePlayerAsync("device1");
        var player2Result = await _repository.CreatePlayerAsync("device2");

        await _repository.AddFriendshipAsync(player2Result.Value, player1Result.Value);

        var friendship = await _context.Friendships.FirstOrDefaultAsync();
        Assert.NotNull(friendship);
        Assert.True(friendship.PlayerId1 < friendship.PlayerId2);
    }

    [Fact]
    public async Task GetFriendIdsAsync_ShouldReturnBidirectionalFriends()
    {
        var player1Result = await _repository.CreatePlayerAsync("device1");
        var player2Result = await _repository.CreatePlayerAsync("device2");
        await _repository.AddFriendshipAsync(player1Result.Value, player2Result.Value);

        var friendsOfPlayer1 = await _repository.GetFriendIdsAsync(player1Result.Value);
        var friendsOfPlayer2 = await _repository.GetFriendIdsAsync(player2Result.Value);

        Assert.Contains(player2Result.Value, friendsOfPlayer1.Value!);
        Assert.Contains(player1Result.Value, friendsOfPlayer2.Value!);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenActionFails_ShouldRollback()
    {
        var playerResult = await _repository.CreatePlayerAsync("device");
        var playerId = playerResult.Value;
        await _repository.UpdateResourceAsync(playerId, ResourceType.Coins, 100);

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            await _repository.UpdateResourceAsync(playerId, ResourceType.Coins, 50);
            return Result.Failure(new Error("Test.Error", "Simulated failure"));
        });

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
