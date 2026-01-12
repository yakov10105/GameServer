namespace GameServer.Domain.Entities;

public sealed class Resource
{
    public Guid PlayerId { get; private set; }
    public ResourceType Type { get; private set; }
    public long Amount { get; private set; }

    public Player Player { get; private set; } = null!;

    private Resource()
    {
    }

    public Resource(Guid playerId, ResourceType type, long amount)
    {
        if (amount < 0)
            throw new ArgumentException("Resource amount cannot be negative", nameof(amount));

        PlayerId = playerId;
        Type = type;
        Amount = amount;
    }

    public void UpdateAmount(long newAmount)
    {
        if (newAmount < 0)
            throw new ArgumentException("Resource amount cannot be negative", nameof(newAmount));

        Amount = newAmount;
    }
}

