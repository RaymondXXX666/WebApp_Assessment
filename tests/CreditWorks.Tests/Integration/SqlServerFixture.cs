using CreditWorks.Web.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace CreditWorks.Tests.Integration;

public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (!SqlServerFixture.Enabled) Skip = SqlServerFixture.SkipReason;
    }
}

public sealed class SqlTheoryAttribute : TheoryAttribute
{
    public SqlTheoryAttribute()
    {
        if (!SqlServerFixture.Enabled) Skip = SqlServerFixture.SkipReason;
    }
}

[CollectionDefinition("SQL Server")]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;

public sealed class SqlServerFixture : IAsyncLifetime
{
    internal const string SkipReason = "Set CREDITWORKS_RUN_SQL_TESTS=1 to run isolated SQL Server tests.";
    internal static bool Enabled => Environment.GetEnvironmentVariable("CREDITWORKS_RUN_SQL_TESTS") == "1";

    private readonly string databaseName = $"CreditWorksTests_{Guid.NewGuid():N}";
    private string? connectionString;

    public async Task InitializeAsync()
    {
        if (!Enabled) return;

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(AppDbContext).Assembly, optional: true)
            .Build();
        var configured = Environment.GetEnvironmentVariable("CREDITWORKS_TEST_CONNECTION_STRING")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "Configure CREDITWORKS_TEST_CONNECTION_STRING or the web project's DefaultConnection user secret.");

        // Always replace the configured database. Never migrate, reset or delete the application database.
        var builder = new SqlConnectionStringBuilder(configured)
        {
            InitialCatalog = databaseName,
            AttachDBFilename = "",
            Pooling = false
        };
        connectionString = builder.ConnectionString;

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public AppDbContext CreateContext(params IInterceptor[] interceptors)
    {
        if (connectionString is null ||
            new SqlConnectionStringBuilder(connectionString).InitialCatalog != databaseName)
            throw new InvalidOperationException("An isolated test database is required.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(interceptors)
            .Options;
        return new AppDbContext(options);
    }

    public async Task ResetAsync()
    {
        if (!Enabled) return;
        await using var db = CreateContext();
        await db.Vehicles.ExecuteDeleteAsync();
        await db.VehicleCategories.ExecuteDeleteAsync();
        await db.Manufacturers.ExecuteDeleteAsync();
        DatabaseSeeder.Seed(db);
    }

    public async Task DisposeAsync()
    {
        if (connectionString is null) return;
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }
}

// xUnit serializes tests in this collection; each test starts with only the seed data.
[Collection("SQL Server")]
[Trait("Category", "Integration")]
public abstract class SqlServerTest(SqlServerFixture database) : IAsyncLifetime
{
    protected SqlServerFixture Database { get; } = database;
    public Task InitializeAsync() => Database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
