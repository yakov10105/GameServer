using GameServer.Api.Configuration;
using GameServer.Infrastructure.Persistence.Context;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.Configure<GameServerOptions>(
    builder.Configuration.GetSection(GameServerOptions.SectionName));

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    dbContext.Database.EnsureCreated();
}

var startTime = DateTimeOffset.UtcNow;

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    activeConnections = WebSocketMiddleware.ActiveConnections,
    uptime = (DateTimeOffset.UtcNow - startTime).ToString(@"dd\.hh\:mm\:ss")
}));

app.UseWebSockets();
app.UseMiddleware<WebSocketMiddleware>();

app.Run();
