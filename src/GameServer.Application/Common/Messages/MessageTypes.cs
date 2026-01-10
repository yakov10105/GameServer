namespace GameServer.Application.Common.Messages;

public static class MessageTypes
{
    public const string Login = "LOGIN";
    public const string UpdateResources = "UPDATE_RESOURCES";
    public const string SendGift = "SEND_GIFT";
    public const string AddFriend = "ADD_FRIEND";
    
    public const string LoginResponse = "LOGIN_RESPONSE";
    public const string ResourceUpdated = "RESOURCE_UPDATED";
    public const string GiftReceived = "GIFT_RECEIVED";
    public const string FriendAdded = "FRIEND_ADDED";
    public const string FriendOnline = "FRIEND_ONLINE";
    public const string Error = "ERROR";
}

