namespace GameServer.Api.Configuration;

public sealed class GameServerOptions
{
    public const string SectionName = "GameServer";
    public int MaxConnections { get; init; } = 1000;
    public int LatencyThresholdMs { get; init; } = 50;
    public int MaxMessageSizeBytes { get; init; } = 1024 * 1024;
}

