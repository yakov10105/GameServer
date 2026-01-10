namespace GameServer.Application.Features.Gameplay.Responses;

public readonly record struct ResourceUpdatedPayload(ResourceType Type, long NewBalance);
