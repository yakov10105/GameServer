namespace GameServer.Application.Features.Auth;

public readonly record struct LoginResponse(Guid PlayerId)
{
    public string Type => "LOGIN_RESPONSE";
}

