namespace GameServer.Application.Features.Gameplay.Requests;

public readonly record struct UpdateResourceRequest(ResourceType Type, long Value);
