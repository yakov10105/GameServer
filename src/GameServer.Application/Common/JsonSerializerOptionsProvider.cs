using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameServer.Application.Common;

public static class JsonSerializerOptionsProvider
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

