namespace GameServer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        
        services.AddKeyedScoped<IMessageHandler, LoginHandler>(MessageTypes.Login);
        services.AddKeyedScoped<IMessageHandler, ResourceHandler>(MessageTypes.UpdateResources);
        services.AddKeyedScoped<IMessageHandler, GiftHandler>(MessageTypes.SendGift);
        services.AddKeyedScoped<IMessageHandler, AddFriendHandler>(MessageTypes.AddFriend);
        
        return services;
    }
}

