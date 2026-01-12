namespace GameServer.Domain.Entities;

public sealed class Player
{
    public Guid Id { get; private set; }
    public string DeviceId { get; private set; }
    public DateTimeOffset LastLogin { get; private set; }

    private readonly List<Resource> _resources = [];
    public IReadOnlyList<Resource> Resources => _resources.AsReadOnly();

    private Player()
    {
        DeviceId = string.Empty;
    }

    public Player(Guid id, string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId cannot be null or empty", nameof(deviceId));

        Id = id;
        DeviceId = deviceId;
        LastLogin = DateTimeOffset.UtcNow;
    }

    public void AddResource(Resource resource)
    {
        _resources.Add(resource);
    }
}

