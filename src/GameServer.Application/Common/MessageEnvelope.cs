namespace GameServer.Application.Common;

public sealed record MessageEnvelope
{
    public string Type { get; init; } = string.Empty;
    public JsonElement Payload { get; init; }
}