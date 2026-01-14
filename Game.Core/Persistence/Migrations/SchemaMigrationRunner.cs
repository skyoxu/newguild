using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Ports;

namespace Game.Core.Persistence.Migrations;

public static class SchemaMigrationRunner
{
    public static async Task EnsureLatestAsync(
        ISQLiteDatabase db,
        int latestVersion,
        IReadOnlyDictionary<int, Func<ISQLiteDatabase, Task>> migrations)
    {
        if (db is null) throw new ArgumentNullException(nameof(db));
        if (latestVersion < 0) throw new ArgumentOutOfRangeException(nameof(latestVersion));
        if (migrations is null) throw new ArgumentNullException(nameof(migrations));

        await db.ExecuteNonQueryAsync(SqlStatement.NoParameters(
            "CREATE TABLE IF NOT EXISTS schema_version (id INTEGER PRIMARY KEY CHECK (id = 1), version INTEGER NOT NULL)"
        ));

        var scalar = await db.ExecuteScalarAsync(SelectSchemaVersionStatement());
        if (scalar is null)
        {
            await db.ExecuteNonQueryAsync(InsertSchemaVersionStatement(version: 0));
            scalar = 0;
        }

        var current = Convert.ToInt32(scalar);
        if (current < 0) throw new InvalidOperationException("schema_version.version must be >= 0.");
        if (current > latestVersion)
            throw new InvalidOperationException($"schema_version.version ({current}) is newer than latestVersion ({latestVersion}).");

        if (current == latestVersion) return;

        for (var nextVersion = current + 1; nextVersion <= latestVersion; nextVersion++)
        {
            if (!migrations.TryGetValue(nextVersion, out var applyMigration))
                throw new InvalidOperationException($"Missing schema migration step for version {nextVersion}.");

            await applyMigration(db);
            await db.ExecuteNonQueryAsync(UpdateSchemaVersionStatement(version: nextVersion));
        }
    }

    private static SqlStatement SelectSchemaVersionStatement()
    {
        return SqlStatement.WithParameters(
            "SELECT version FROM schema_version WHERE id = @id",
            new Dictionary<string, object?> { ["@id"] = 1 });
    }

    private static SqlStatement InsertSchemaVersionStatement(int version)
    {
        return SqlStatement.WithParameters(
            "INSERT INTO schema_version(id, version) VALUES(@id,@version)",
            new Dictionary<string, object?>
            {
                ["@id"] = 1,
                ["@version"] = version,
            });
    }

    private static SqlStatement UpdateSchemaVersionStatement(int version)
    {
        return SqlStatement.WithParameters(
            "UPDATE schema_version SET version=@version WHERE id = 1",
            new Dictionary<string, object?>
            {
                ["@version"] = version,
            });
    }
}
