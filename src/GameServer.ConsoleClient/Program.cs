


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(LogEventLevel.Debug)
    .WriteTo.File("logs/console-client-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddSingleton<GameClient>();
builder.Services.AddSingleton<InteractiveCliService>();

using var host = builder.Build();

var cliService = host.Services.GetRequiredService<InteractiveCliService>();

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (s, e) => {
    e.Cancel = true; 
    cts.Cancel();    
};

try
{
    return await cliService.RunAsync(args,cts.Token);
}
finally
{
    var client = host.Services.GetRequiredService<GameClient>();
    await client.DisposeAsync();
    await Log.CloseAndFlushAsync();
}

