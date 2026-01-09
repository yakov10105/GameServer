using GameServer.Infrastructure.Concurrency;
using GameServer.Infrastructure.Network;
using GameServer.Infrastructure.Persistence.Context;
using GameServer.Infrastructure.Persistence.Repositories;
using GameServer.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services.AddDbContext<GameDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));

        services.AddSingleton<ISessionManager, InMemorySessionManager>();
        services.AddSingleton<ISynchronizationProvider, LocalSemaphoreProvider>();
        services.AddScoped<IStateRepository, SqliteStateRepository>();
        services.AddScoped<IGameNotifier, WebSocketDirectNotifier>();

        return services;
    }
}
