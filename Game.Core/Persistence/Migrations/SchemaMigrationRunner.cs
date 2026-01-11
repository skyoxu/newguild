using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Ports;

namespace Game.Core.Persistence.Migrations;

public static class SchemaMigrationRunner
{
    public static async Task EnsureLatestAsync(ISQLiteDatabase db, int latestVersion)
    {
        if (db is null) throw new ArgumentNullException(nameof(db));
        if (latestVersion < 0) throw new ArgumentOutOfRangeException(nameof(latestVersion));

        await db.ExecuteNonQueryAsync(SqlStatement.NoParameters(
            "CREATE TABLE IF NOT EXISTS schema_version (id INTEGER PRIMARY KEY CHECK (id = 1), version INTEGER NOT NULL)"
        ));

        var scalar = await db.ExecuteScalarAsync(SqlStatement.WithParameters(
            "SELECT version FROM schema_version WHERE id = @id",
            new Dictionary<string, object?>
            {
                ["@id"] = 1
            }
        ));
        if (scalar is null)
        {
            await db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
                "INSERT INTO schema_version(id, version) VALUES(1,@version)",
                new Dictionary<string, object?>
                {
                    ["@version"] = latestVersion,
                }
            ));
            return;
        }

        var current = Convert.ToInt32(scalar);
        if (current >= latestVersion) return;

        await db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
            "UPDATE schema_version SET version=@version WHERE id = 1",
            new Dictionary<string, object?>
            {
                ["@version"] = latestVersion,
            }
        ));
    }
}
