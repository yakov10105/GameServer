namespace GameServer.ConsoleClient.Model.Request;

internal readonly record struct UpdateResourceRequest(int Type, long Value) : IGameRequest
{
    public static string MessageType => "UPDATE_RESOURCES";
}
