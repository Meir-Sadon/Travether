using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Travether.Api.Data;

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

/// <summary>The real API pointed at the test container.</summary>
public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Default", connectionString);
}
