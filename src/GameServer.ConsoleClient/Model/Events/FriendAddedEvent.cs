namespace GameServer.ConsoleClient.Model.Events;

public readonly record struct FriendAddedEvent(Guid ByPlayerId) : IServerEvent
{
    public readonly string Type => "FRIEND_ADDED";
}

