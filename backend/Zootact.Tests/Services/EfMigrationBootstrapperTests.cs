using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Zootact.API.Services;

namespace Zootact.Tests.Services;

public sealed class EfMigrationBootstrapperTests
{
    [Fact]
    public void GetLegacySchemaCompatibilityStatements_ReturnsPostgresColumnWidening()
    {
        var statements = EfMigrationBootstrapper.GetLegacySchemaCompatibilityStatements("Npgsql.EntityFrameworkCore.PostgreSQL");

        Assert.Contains(statements, statement => statement.Contains("ALTER TABLE users ALTER COLUMN email TYPE character varying(512)", StringComparison.Ordinal));
        Assert.Contains(statements, statement => statement.Contains("ALTER TABLE users ALTER COLUMN avatar_url TYPE character varying(2048)", StringComparison.Ordinal));
    }

    [Fact]
    public void GetLegacySchemaCompatibilityStatements_SkipsSqlite()
    {
        var statements = EfMigrationBootstrapper.GetLegacySchemaCompatibilityStatements("Microsoft.EntityFrameworkCore.Sqlite");

        Assert.Empty(statements);
    }

    [Fact]
    public async Task EnsureLegacyMigrationBaselineAsync_LeavesConnectionUsableForSubsequentOperations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LegacyBootstrapDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new LegacyBootstrapDbContext(options);
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE users (
                id TEXT PRIMARY KEY
            );
            """);

        await EfMigrationBootstrapper.EnsureLegacyMigrationBaselineAsync(
            dbContext.Database,
            ["20260418060050_InitialSchema"]);

        var exception = await Record.ExceptionAsync(() => dbContext.Database.ExecuteSqlRawAsync("SELECT 1;"));

        Assert.Null(exception);

        await using var command = connection.CreateCommand();
        command.CommandText = """SELECT COUNT(*) FROM "__EFMigrationsHistory";""";
        var historyCount = (long)(await command.ExecuteScalarAsync() ?? 0L);

        Assert.Equal(1L, historyCount);
    }

    private sealed class LegacyBootstrapDbContext(DbContextOptions<LegacyBootstrapDbContext> options) : DbContext(options);
}
