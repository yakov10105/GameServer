namespace GameServer.Application.Features.Gameplay.Requests;

public readonly record struct SendGiftRequest(Guid FriendPlayerId, ResourceType Type, long Value);
