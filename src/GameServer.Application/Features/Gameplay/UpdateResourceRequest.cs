namespace GameServer.Application.Features.Gameplay;

public readonly record struct UpdateResourceRequest(ResourceType Type, long Value);

