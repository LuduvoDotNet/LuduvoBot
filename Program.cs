using LuduvoDotNet;
using LuduvoBot.Modules;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Rest;
var builder=Host.CreateApplicationBuilder(args);
builder.Services.AddDiscordGateway(options =>
{
    options.Token=Environment.GetEnvironmentVariable("DISCORD_TOKEN")??throw new InvalidOperationException("DISCORD_TOKEN environment variable is not set.");
    options.Intents=GatewayIntents.All;
}).AddApplicationCommands();
var app=builder.Build();
app.AddApplicationCommandModule<UserModule>();
app.AddApplicationCommandModule<PlacesModule>();
app.AddApplicationCommandModule<VerificationModule>();
await app.RunAsync();