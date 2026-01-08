using GameServer.Domain.Enums;

namespace GameServer.Application.Features.Gameplay;

public readonly record struct UpdateResourceResponse(ResourceType Type, long NewBalance);

