namespace GameServer.Domain.Entities;

public sealed class Friendship
{
    public Guid PlayerId1 { get; private set; }
    public Guid PlayerId2 { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Player Player1 { get; private set; } = null!;
    public Player Player2 { get; private set; } = null!;

    private Friendship()
    {
    }

    public Friendship(Guid playerId1, Guid playerId2)
    {
        if (playerId1 == playerId2)
            throw new ArgumentException("A player cannot be friends with themselves");

        if (playerId1 < playerId2)
        {
            PlayerId1 = playerId1;
            PlayerId2 = playerId2;
        }
        else
        {
            PlayerId1 = playerId2;
            PlayerId2 = playerId1;
        }

        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid GetOtherPlayer(Guid playerId)
    {
        if (PlayerId1 == playerId)
            return PlayerId2;
        if (PlayerId2 == playerId)
            return PlayerId1;

        throw new ArgumentException("PlayerId is not part of this friendship", nameof(playerId));
    }
}

