using System.Text.Json.Serialization;

namespace GameServer.ConsoleClient.Model.Events;

public readonly record struct ResourceUpdatedEvent : IServerEvent
{
    [JsonPropertyName("type")]
    public int ResourceType { get; init; }
    
    public long NewBalance { get; init; }
    
    [JsonIgnore]
    public readonly string Type => "RESOURCE_UPDATED";
    
    public string TypeName => ResourceType switch
    {
        0 => "Coins",
        1 => "Rolls",
        _ => "Unknown"
    };
}
