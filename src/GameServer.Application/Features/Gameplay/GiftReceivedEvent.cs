using GameServer.Domain.Enums;

namespace GameServer.Application.Features.Gameplay;

public readonly record struct GiftReceivedEvent(Guid FromPlayerId, ResourceType Type, long Amount);

