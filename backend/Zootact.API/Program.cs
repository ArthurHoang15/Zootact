using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Zootact.API.Hubs;
using Zootact.API.Middleware;
using Zootact.API.Services;
using Zootact.Core.Interfaces;
using Zootact.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var frontendUrl = builder.Configuration["Frontend:Url"];
var postgresConnection = builder.Configuration.GetConnectionString("PostgreSQL");
var redisConnection = builder.Configuration.GetConnectionString("Redis");
var firebaseConfigPath = builder.Configuration["Firebase:ServiceAccountKeyPath"] ?? "Config/firebase-adminsdk.json";

if (string.IsNullOrWhiteSpace(frontendUrl))
    throw new InvalidOperationException("Missing required configuration: Frontend:Url");
if (string.IsNullOrWhiteSpace(postgresConnection))
    throw new InvalidOperationException("Missing required connection string: PostgreSQL");
if (string.IsNullOrWhiteSpace(redisConnection))
    throw new InvalidOperationException("Missing required connection string: Redis");
if (!File.Exists(firebaseConfigPath))
    throw new InvalidOperationException($"Firebase service account file not found: {firebaseConfigPath}");

FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromFile(firebaseConfigPath)
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGameLogic();
builder.Services.AddScoped<IMatchNotificationService, SignalRMatchNotificationService>();
builder.Services.AddScoped<IPrivateLobbyNotificationService, SignalRPrivateLobbyNotificationService>();

builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = "Zootact";
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(frontendUrl)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseFirebaseAuth();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/game-hub");

app.Run();
