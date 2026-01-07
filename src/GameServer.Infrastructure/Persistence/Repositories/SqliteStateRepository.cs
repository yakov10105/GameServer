using GameServer.Domain.Entities;
using GameServer.Infrastructure.Persistence.Context;

namespace GameServer.Infrastructure.Persistence.Repositories;

public sealed class SqliteStateRepository(GameDbContext context) : IStateRepository
{
    private readonly GameDbContext _context = context;

    private static readonly Func<GameDbContext, string, Task<Player?>> GetPlayerByDeviceIdQuery =
        EF.CompileAsyncQuery((GameDbContext ctx, string deviceId) =>
            ctx.Players
                .AsNoTracking()
                .FirstOrDefault(p => p.DeviceId == deviceId));

    private static readonly Func<GameDbContext, Guid, ResourceType, Task<Resource?>> GetResourceQuery =
        EF.CompileAsyncQuery((GameDbContext ctx, Guid playerId, ResourceType resourceType) =>
            ctx.Resources
                .AsNoTracking()
                .FirstOrDefault(r => r.PlayerId == playerId && r.Type == resourceType));

    private static readonly Func<GameDbContext, Guid, IAsyncEnumerable<Friendship>> GetFriendshipsQuery =
        EF.CompileAsyncQuery((GameDbContext ctx, Guid playerId) =>
            ctx.Friendships
                .AsNoTracking()
                .Where(f => f.PlayerId1 == playerId || f.PlayerId2 == playerId));

    public async Task<Result<Guid>> CreatePlayerAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var existingPlayer = await GetPlayerByDeviceIdQuery(_context, deviceId);

            if (existingPlayer != null)
                return Result<Guid>.Success(existingPlayer.Id);

            var playerId = Guid.NewGuid();
            var player = new Player(playerId, deviceId);

            player.AddResource(new Resource(playerId, ResourceType.Coins, 0));
            player.AddResource(new Resource(playerId, ResourceType.Rolls, 0));

            _context.Players.Add(player);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(playerId);
        }
        catch (Exception ex)
        {
            return Result<Guid>.Failure(new Error("CreatePlayer.Failed", ex.Message));
        }
    }

    public async Task<Result<Guid>> GetPlayerIdByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var player = await GetPlayerByDeviceIdQuery(_context, deviceId);

            if (player == null)
                return Result<Guid>.Failure(new Error("Player.NotFound", $"Player with DeviceId '{deviceId}' not found"));

            return Result<Guid>.Success(player.Id);
        }
        catch (Exception ex)
        {
            return Result<Guid>.Failure(new Error("GetPlayerIdByDeviceId.Failed", ex.Message));
        }
    }

    public async Task<Result<long>> GetResourceAmountAsync(Guid playerId, ResourceType resourceType, CancellationToken cancellationToken = default)
    {
        try
        {
            var resource = await GetResourceQuery(_context, playerId, resourceType);

            if (resource == null)
                return Result<long>.Failure(new Error("Resource.NotFound", $"Resource {resourceType} not found for player {playerId}"));

            return Result<long>.Success(resource.Amount);
        }
        catch (Exception ex)
        {
            return Result<long>.Failure(new Error("GetResourceAmount.Failed", ex.Message));
        }
    }

    public async Task<Result> UpdateResourceAsync(Guid playerId, ResourceType resourceType, long amount, CancellationToken cancellationToken = default)
    {
        try
        {
            var resource = await _context.Resources
                .FirstOrDefaultAsync(r => r.PlayerId == playerId && r.Type == resourceType, cancellationToken);

            if (resource == null)
                return Result.Failure(new Error("Resource.NotFound", $"Resource {resourceType} not found for player {playerId}"));

            resource.UpdateAmount(amount);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("UpdateResource.Failed", ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<Guid>>> GetFriendIdsAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var friendships = new List<Friendship>();
            await foreach (var friendship in GetFriendshipsQuery(_context, playerId))
            {
                friendships.Add(friendship);
            }

            var friendIds = friendships
                .Select(f => f.GetOtherPlayer(playerId))
                .ToList();

            return Result<IReadOnlyList<Guid>>.Success(friendIds.AsReadOnly());
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Guid>>.Failure(new Error("GetFriendIds.Failed", ex.Message));
        }
    }

    public async Task<Result> AddFriendshipAsync(Guid playerId1, Guid playerId2, CancellationToken cancellationToken = default)
    {
        try
        {
            var player1Exists = await _context.Players
                .AsNoTracking()
                .AnyAsync(p => p.Id == playerId1, cancellationToken);

            var player2Exists = await _context.Players
                .AsNoTracking()
                .AnyAsync(p => p.Id == playerId2, cancellationToken);

            if (!player1Exists || !player2Exists)
                return Result.Failure(new Error("Player.NotFound", "One or both players not found"));

            var friendship = new Friendship(playerId1, playerId2);

            var existingFriendship = await _context.Friendships
                .AsNoTracking()
                .FirstOrDefaultAsync(f => 
                    f.PlayerId1 == friendship.PlayerId1 && 
                    f.PlayerId2 == friendship.PlayerId2, 
                    cancellationToken);

            if (existingFriendship != null)
                return Result.Failure(new Error("Friendship.AlreadyExists", "Friendship already exists"));

            _context.Friendships.Add(friendship);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("AddFriendship.Failed", ex.Message));
        }
    }

    public async Task<Result> ExecuteInTransactionAsync(Func<Task<Result>> action, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var result = await action();
            
            if (!result.IsSuccess)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(new Error("Transaction.Failed", ex.Message));
        }
    }
}

