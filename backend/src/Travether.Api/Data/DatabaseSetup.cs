using Microsoft.EntityFrameworkCore;

namespace Travether.Api.Data;

public static class DatabaseSetup
{
    public static IServiceCollection AddTravetherDatabase(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not set.");

        services.AddDbContext<TravetherDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
            .UseSnakeCaseNamingConvention());

        return services;
    }

    /// <summary>Applies pending migrations when Database:MigrateOnStartup is true (Render sets it).</summary>
    public static async Task MigrateIfConfiguredAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TravetherDbContext>();
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }
}
