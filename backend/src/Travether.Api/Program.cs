using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Travether.Api.Auth;
using Travether.Api.Authorization;
using Travether.Api.Cards;
using Travether.Api.Chat;
using Travether.Api.Data;
using Travether.Api.Email;
using Travether.Api.Images;
using Travether.Api.Notifications;
using Travether.Api.Places;
using Travether.Api.Plans;
using Travether.Api.Privacy;
using Travether.Api.Profiles;
using Travether.Api.Reviews;
using Travether.Api.Safety;
using Travether.Api.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Render (and docker compose) pass the port in PORT; locally launchSettings.json sets the URL.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Enums travel as camelCase strings ("coAdmin", "groupsOnly"), matching the frontend types.
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddTravetherDatabase(builder.Configuration);
builder.Services.AddScoped<AccessQueries>();
builder.Services.AddScoped<RatingQueries>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<CardViews>();
builder.Services.AddScoped<PlanViews>();
builder.Services.AddHostedService<PlanRequestSweeper>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, UserIdProvider>();
builder.Services.AddTravetherPlaces(builder.Configuration);
builder.Services.AddTravetherImages(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTravetherEmail(builder.Configuration);
builder.Services.AddTravetherNotifications(builder.Configuration, builder.Environment);
builder.Services.AddTravetherSafety(builder.Configuration);
builder.Services.AddTravetherPrivacy(builder.Configuration);
builder.Services.AddTravetherTelemetry(builder.Configuration, builder.Environment);
builder.WebHost.UseTravetherSentry(builder.Configuration, builder.Environment);
builder.Services.AddTravetherAuth(builder.Configuration, builder.Environment);

if (builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    // Render terminates TLS at its proxy; trust X-Forwarded-* so Request.IsHttps and client IPs are correct.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

await app.MigrateIfConfiguredAsync();

if (app.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<CsrfMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// The built frontend is copied into wwwroot by the Dockerfile, so one service serves both.
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseLocalImages();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<ChatHub>(ChatHub.Path);

// Client-side routes fall back to index.html; unknown /api routes stay 404.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.RunAsync();

public partial class Program;
