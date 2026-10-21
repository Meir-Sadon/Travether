using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;
using Travether.Api.Auth;
using Travether.Api.Data;
using Travether.Api.Email;

namespace Travether.Api.Tests;

/// <summary>One PostGIS container per test run, migrated once. Tests seed their own rows with fresh ids.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same major versions as docker-compose.yml.
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgis/postgis:17-3.5").Build();

    public string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    public TravetherDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TravetherDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.UseNetTopologySuite())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new TravetherDbContext(options);
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseTests : ICollectionFixture<PostgresFixture>
{
    public const string Name = "database";
}

/// <summary>
/// The real API pointed at the test container. Emails are captured instead of sent, Google/Apple
/// tokens are checked by <see cref="FakeExternalVerifier"/>, and the auth rate limit is lifted.
/// </summary>
public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public CapturingEmailSender Emails { get; } = new();

    public FakeExternalVerifier External { get; } = new();

    /// <summary>A client that keeps cookies and sends the CSRF header, like the frontend.</summary>
    public HttpClient CreateApiClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(CsrfMiddleware.HeaderName, "1");
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("RateLimits:AuthPerMinute", "100000");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.RemoveAll<IExternalIdentityVerifier>();
            services.AddSingleton<IExternalIdentityVerifier>(External);
        });
    }
}
