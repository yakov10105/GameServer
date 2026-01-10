using GameServer.Domain.Enums;

namespace GameServer.Application.Features.Gameplay.Events;

public readonly record struct GiftReceivedPayload(
    Guid FromPlayerId,
    ResourceType ResourceType,
    long Amount);

