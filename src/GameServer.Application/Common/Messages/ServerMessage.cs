namespace GameServer.Application.Common.Messages;

public readonly record struct ServerMessage<TPayload>(string Type, TPayload Payload);

