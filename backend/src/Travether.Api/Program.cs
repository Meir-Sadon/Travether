using Microsoft.AspNetCore.HttpOverrides;
using Travether.Api.Authorization;
using Travether.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Render (and docker compose) pass the port in PORT; locally launchSettings.json sets the URL.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddTravetherDatabase(builder.Configuration);
builder.Services.AddScoped<AccessQueries>();
builder.Services.AddSingleton(TimeProvider.System);

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// The built frontend is copied into wwwroot by the Dockerfile, so one service serves both.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// Client-side routes fall back to index.html; unknown /api routes stay 404.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.RunAsync();

public partial class Program;
