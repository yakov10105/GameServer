namespace GameServer.ConsoleClient.Model.Request;

internal readonly record struct AddFriendRequest(Guid FriendPlayerId) : IGameRequest
{
    public static string MessageType => "ADD_FRIEND";
}

