namespace GameServer.ConsoleClient.Model.Events;

public readonly record struct FriendOnlineEvent(Guid FriendPlayerId) : IServerEvent
{
    public readonly string Type => "FRIEND_ONLINE";
}
