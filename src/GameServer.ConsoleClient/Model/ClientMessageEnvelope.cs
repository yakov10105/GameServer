namespace GameServer.ConsoleClient.Model;

internal readonly record struct ClientMessageEnvelope(string Type, object Payload);
