namespace GameServer.ConsoleClient.Model.Request;

internal readonly record struct SendGiftRequest(Guid FriendPlayerId, int Type, long Value) : IGameRequest
{
    public static string MessageType => "SEND_GIFT";
}
