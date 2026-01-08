using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        
        // TODO Phase 4: Add handlers when created
        // services.AddKeyedScoped<IMessageHandler, LoginHandler>("LOGIN");
        // services.AddKeyedScoped<IMessageHandler, ResourceHandler>("UPDATE_RESOURCES");
        // services.AddKeyedScoped<IMessageHandler, GiftHandler>("SEND_GIFT");
        
        return services;
    }
}

