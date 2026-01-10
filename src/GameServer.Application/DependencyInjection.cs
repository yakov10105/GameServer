using GameServer.Application.Features.Auth.Handlers;
using GameServer.Application.Features.Gameplay.Handlers;
using GameServer.Application.Features.Social.Handlers;

namespace GameServer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        
        services.AddKeyedScoped<IMessageHandler, LoginHandler>("LOGIN");
        services.AddKeyedScoped<IMessageHandler, ResourceHandler>("UPDATE_RESOURCES");
        services.AddKeyedScoped<IMessageHandler, GiftHandler>("SEND_GIFT");
        services.AddKeyedScoped<IMessageHandler, AddFriendHandler>("ADD_FRIEND");
        
        return services;
    }
}

