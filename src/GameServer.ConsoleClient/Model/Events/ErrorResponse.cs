namespace GameServer.ConsoleClient.Model.Events;

public record struct ErrorResponse(string Code, string Message) : IServerEvent
{
    public readonly string Type => "ERROR";
}
