namespace GameServer.ConsoleClient.Model.Events;

public readonly record struct ServerMessageEnvelope(string Type, JsonElement Payload);
