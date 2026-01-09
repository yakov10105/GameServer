namespace GameServer.ConsoleClient.Model.Events;

public record struct LoginResponse(Guid PlayerId) : IServerEvent
{
    public readonly string Type => "LOGIN_RESPONSE";
}
