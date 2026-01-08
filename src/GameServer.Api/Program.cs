

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
     .ReadFrom.Configuration(context.Configuration);
});

builder .Services.AddApplication();
builder.Services.AddInfrastructure();

var app = builder.Build();

app.UseWebSockets();
app.UseMiddleware<WebSocketMiddleware>();