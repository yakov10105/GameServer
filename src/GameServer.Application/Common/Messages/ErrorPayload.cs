namespace GameServer.Application.Common.Messages;

public readonly record struct ErrorPayload(string Code, string Message);

