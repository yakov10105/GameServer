namespace GameServer.ConsoleClient.Model.Request;

internal readonly record struct LoginRequest(string DeviceId) : IGameRequest
{
    public static string MessageType => "LOGIN";
}
