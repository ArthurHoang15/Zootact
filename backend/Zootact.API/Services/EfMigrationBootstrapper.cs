using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Zootact.API.Services;

public static class EfMigrationBootstrapper
{
    public static IReadOnlyList<string> GetLegacySchemaCompatibilityStatements(string? providerName)
    {
        if (providerName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true)
        {
            return [];
        }

        return
        [
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'users'
                      AND column_name = 'email'
                      AND character_maximum_length IS NOT NULL
                      AND character_maximum_length < 512
                ) THEN
                    ALTER TABLE users ALTER COLUMN email TYPE character varying(512);
                END IF;
            END $$;
            """,
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'users'
                      AND column_name = 'avatar_url'
                      AND character_maximum_length IS NOT NULL
                      AND character_maximum_length < 2048
                ) THEN
                    ALTER TABLE users ALTER COLUMN avatar_url TYPE character varying(2048);
                END IF;
            END $$;
            """
        ];
    }

    public static async Task EnsureLegacyMigrationBaselineAsync(
        DatabaseFacade database,
        IEnumerable<string> migrationIds,
        CancellationToken cancellationToken = default)
    {
        var connection = database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
        {
            await database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            var historyExists = await TableExistsAsync(database, connection, "__EFMigrationsHistory", cancellationToken);
            var usersTableExists = await TableExistsAsync(database, connection, "users", cancellationToken);

            if (historyExists || !usersTableExists)
            {
                return;
            }

            foreach (var statement in GetLegacySchemaCompatibilityStatements(database.ProviderName))
            {
                await database.ExecuteSqlRawAsync(statement, cancellationToken);
            }

            await database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL,
                    "ProductVersion" TEXT NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );
                """, cancellationToken);

            foreach (var migrationId in migrationIds)
            {
                await database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    SELECT {migrationId}, {"8.0.0"}
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM "__EFMigrationsHistory"
                        WHERE "MigrationId" = {migrationId}
                    );
                    """, cancellationToken);
            }
        }
        finally
        {
            if (openedHere)
            {
                await database.CloseConnectionAsync();
            }
        }
    }

    private static async Task<bool> TableExistsAsync(
        DatabaseFacade database,
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        if (database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @tableName)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);
        }
        else
        {
            command.CommandText = "SELECT to_regclass(@tableName) IS NOT NULL";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "tableName";
            parameter.Value = $"public.{tableName}";
            command.Parameters.Add(parameter);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result switch
        {
            bool booleanResult => booleanResult,
            byte byteResult => byteResult != 0,
            short shortResult => shortResult != 0,
            int intResult => intResult != 0,
            long longResult => longResult != 0,
            _ => result is not null && Convert.ToInt64(result) != 0,
        };
    }
}
