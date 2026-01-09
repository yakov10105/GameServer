using GameServer.Application.Features.Auth;
using GameServer.Application.Features.Gameplay;

namespace GameServer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        
        services.AddKeyedScoped<IMessageHandler, LoginHandler>("LOGIN");
        services.AddKeyedScoped<IMessageHandler, ResourceHandler>("UPDATE_RESOURCES");
        services.AddKeyedScoped<IMessageHandler, GiftHandler>("SEND_GIFT");
        
        return services;
    }
}

