namespace GameServer.ConsoleClient.Model.Events;

public record struct GiftReceivedEvent(Guid FromPlayerId, int ResourceType, long Amount) : IServerEvent
{
    public string Type => "GIFT_RECEIVED";
}
