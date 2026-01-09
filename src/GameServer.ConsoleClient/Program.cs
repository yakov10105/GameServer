using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using GameServer.ConsoleClient.Clients;
using GameServer.ConsoleClient.Services;

var verboseMode = args.Contains("--verbose") || args.Contains("-v");
var filteredArgs = args.Where(a => a != "--verbose" && a != "-v").ToArray();

var logLevelFromEnv = Environment.GetEnvironmentVariable("GAMESERVER_LOG_LEVEL");
var logLevel = verboseMode 
    ? LogEventLevel.Debug 
    : ParseLogLevel(logLevelFromEnv, LogEventLevel.Information);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(logLevel)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(filteredArgs);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddSingleton<GameClient>();
builder.Services.AddSingleton<InteractiveCliService>();

using var host = builder.Build();

var cliService = host.Services.GetRequiredService<InteractiveCliService>();

try
{
    return await cliService.RunAsync(filteredArgs, verboseMode || logLevel == LogEventLevel.Debug);
}
finally
{
    await Log.CloseAndFlushAsync();
}

static LogEventLevel ParseLogLevel(string? value, LogEventLevel defaultLevel)
{
    if (string.IsNullOrWhiteSpace(value))
        return defaultLevel;
    
    return value.ToUpperInvariant() switch
    {
        "DEBUG" or "VERBOSE" => LogEventLevel.Debug,
        "INFO" or "INFORMATION" => LogEventLevel.Information,
        "WARN" or "WARNING" => LogEventLevel.Warning,
        "ERROR" => LogEventLevel.Error,
        _ => defaultLevel
    };
}
