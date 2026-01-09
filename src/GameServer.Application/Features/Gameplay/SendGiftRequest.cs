using GameServer.Domain.Enums;

namespace GameServer.Application.Features.Gameplay;

public readonly record struct SendGiftRequest(Guid FriendPlayerId, ResourceType Type, long Value);

