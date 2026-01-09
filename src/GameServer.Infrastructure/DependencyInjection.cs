using GameServer.Infrastructure.Concurrency;
using GameServer.Infrastructure.Network;
using GameServer.Infrastructure.Persistence.Context;
using GameServer.Infrastructure.Persistence.Repositories;
using GameServer.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        
        services.AddSingleton(connection);
        
        services.AddDbContext<GameDbContext>(options =>
            options.UseSqlite(connection));

        services.AddSingleton<ISessionManager, InMemorySessionManager>();
        services.AddSingleton<ISynchronizationProvider, LocalSemaphoreProvider>();
        services.AddScoped<IStateRepository, SqliteStateRepository>();
        services.AddScoped<IGameNotifier, WebSocketDirectNotifier>();

        return services;
    }
}
