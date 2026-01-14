using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Persistence.Migrations;
using Game.Core.Ports;
using Xunit;

namespace Game.Core.Tests.Persistence.Migrations;

public sealed class SchemaMigrationTests
{
    // ACC:T25.2
    [Fact]
    public async Task EnsureLatestAsync_Should_Create_SchemaVersion_Metadata_And_Set_Version_To_Latest_When_No_Metadata_Exists()
    {
        var db = new RecordingSQLiteDatabase(schemaVersionTableExists: false, schemaVersionRowExists: false, schemaVersion: 0);

        var migrations = new Dictionary<int, Func<ISQLiteDatabase, Task>>
        {
            [1] = database => database.ExecuteNonQueryAsync(SqlStatement.NoParameters("CREATE TABLE IF NOT EXISTS test_table_v1 (id INTEGER PRIMARY KEY)"))
        };

        await SchemaMigrationRunner.EnsureLatestAsync(db, 1, migrations);

        db.SchemaVersionTableExists.Should().BeTrue("migration runner must ensure schema_version metadata exists");
        db.SchemaVersionRowExists.Should().BeTrue("migration runner must insert the schema_version row when it does not exist");
        db.SchemaVersion.Should().Be(1, "migration runner must set schema version to latest when initializing a new database");
        db.ExecutedNonQuerySql.Should().Contain(
            sql => NormalizeSql(sql).Contains("create table if not exists schema_version"),
            "migration runner must create schema_version table");
        db.ExecutedNonQuerySql.Should().Contain(
            sql => NormalizeSql(sql).Contains("insert") && NormalizeSql(sql).Contains("schema_version"),
            "migration runner must insert the single-row schema_version record");
        db.ExecutedNonQuerySql.Should().Contain(
            sql => NormalizeSql(sql).Contains("create table if not exists test_table_v1"),
            "migration runner must execute the migration step for version 1");
    }

    [Fact]
    public async Task EnsureLatestAsync_Should_Upgrade_From_Older_Version_To_Latest()
    {
        var db = new RecordingSQLiteDatabase(schemaVersionTableExists: true, schemaVersionRowExists: true, schemaVersion: 0);

        var migrations = new Dictionary<int, Func<ISQLiteDatabase, Task>>
        {
            [1] = database => database.ExecuteNonQueryAsync(SqlStatement.NoParameters("CREATE TABLE IF NOT EXISTS test_table_v1 (id INTEGER PRIMARY KEY)")),
            [2] = database => database.ExecuteNonQueryAsync(SqlStatement.NoParameters("ALTER TABLE test_table_v1 ADD COLUMN name TEXT"))
        };

        await SchemaMigrationRunner.EnsureLatestAsync(db, 2, migrations);

        db.SchemaVersion.Should().Be(2, "migration runner must upgrade schema version when database is older than latest");
        db.ExecutedNonQuerySql.Should().Contain(
            sql => NormalizeSql(sql).Contains("update") &&
                   NormalizeSql(sql).Contains("schema_version") &&
                   NormalizeSql(sql).Contains("set") &&
                   NormalizeSql(sql).Contains("version") &&
                   NormalizeSql(sql).Contains("@") &&
                   NormalizeSql(sql).Contains("where") &&
                   NormalizeSql(sql).Contains("id = 1"),
            "migration runner must update schema_version.version using parameters to avoid unsafe SQL and target the single-row metadata record");
    }

    [Fact]
    public async Task EnsureLatestAsync_Should_Not_Change_When_Current_Is_Already_Latest()
    {
        var db = new RecordingSQLiteDatabase(schemaVersionTableExists: true, schemaVersionRowExists: true, schemaVersion: 2);

        var migrations = new Dictionary<int, Func<ISQLiteDatabase, Task>>
        {
            [1] = database => database.ExecuteNonQueryAsync(SqlStatement.NoParameters("CREATE TABLE IF NOT EXISTS test_table_v1 (id INTEGER PRIMARY KEY)")),
            [2] = database => database.ExecuteNonQueryAsync(SqlStatement.NoParameters("ALTER TABLE test_table_v1 ADD COLUMN name TEXT"))
        };

        await SchemaMigrationRunner.EnsureLatestAsync(db, 2, migrations);

        db.SchemaVersion.Should().Be(2, "migration runner must be a no-op when database schema is already at latest");
        db.ExecutedNonQuerySql.Should().NotContain(sql => NormalizeSql(sql).StartsWith("insert", StringComparison.Ordinal));
        db.ExecutedNonQuerySql.Should().NotContain(sql => NormalizeSql(sql).StartsWith("update", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnsureLatestAsync_Should_Fail_When_Migration_Step_Is_Missing()
    {
        var db = new RecordingSQLiteDatabase(schemaVersionTableExists: true, schemaVersionRowExists: true, schemaVersion: 0);

        var migrations = new Dictionary<int, Func<ISQLiteDatabase, Task>>
        {
            [1] = database => database.ExecuteNonQueryAsync(SqlStatement.NoParameters("CREATE TABLE IF NOT EXISTS test_table_v1 (id INTEGER PRIMARY KEY)"))
        };

        var act = async () => await SchemaMigrationRunner.EnsureLatestAsync(db, 2, migrations);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Missing schema migration step for version 2*");
    }

    private static string NormalizeSql(string sql)
    {
        if (sql is null) return string.Empty;
        var lowered = sql.Replace("\\r", " ").Replace("\\n", " ").Trim().ToLowerInvariant();
        return Regex.Replace(lowered, "\\\\s+", " ");
    }

    private sealed class RecordingSQLiteDatabase : ISQLiteDatabase
    {
        public RecordingSQLiteDatabase(bool schemaVersionTableExists, bool schemaVersionRowExists, int schemaVersion)
        {
            SchemaVersionTableExists = schemaVersionTableExists;
            SchemaVersionRowExists = schemaVersionRowExists;
            SchemaVersion = schemaVersion;
        }

        public bool SchemaVersionTableExists { get; private set; }
        public bool SchemaVersionRowExists { get; private set; }
        public int SchemaVersion { get; private set; }

        public List<string> ExecutedNonQuerySql { get; } = new();
        public List<string> ExecutedScalarSql { get; } = new();
        public List<string> ExecutedQuerySql { get; } = new();

        public Task<int> ExecuteNonQueryAsync(SqlStatement stmt)
        {
            ExecutedNonQuerySql.Add(stmt.Text);

            var normalized = NormalizeSql(stmt.Text);
            if (normalized.Contains("create table") && normalized.Contains("schema_version"))
            {
                SchemaVersionTableExists = true;
                return Task.FromResult(0);
            }

            if (normalized.StartsWith("insert", StringComparison.Ordinal) && normalized.Contains("schema_version"))
            {
                SchemaVersionTableExists = true;
                SchemaVersionRowExists = true;

                if (TryGetVersionValue(stmt, out var v))
                    SchemaVersion = v;

                return Task.FromResult(1);
            }

            if (normalized.StartsWith("update", StringComparison.Ordinal) && normalized.Contains("schema_version") && normalized.Contains("set") && normalized.Contains("version"))
            {
                if (TryGetVersionValue(stmt, out var v))
                    SchemaVersion = v;
                return Task.FromResult(1);
            }

            return Task.FromResult(0);
        }

        public Task<object?> ExecuteScalarAsync(SqlStatement stmt)
        {
            ExecutedScalarSql.Add(stmt.Text);

            var normalized = NormalizeSql(stmt.Text);
            if (normalized.Contains("schema_version"))
            {
                if (!SchemaVersionTableExists || !SchemaVersionRowExists)
                    return Task.FromResult<object?>(null);

                return Task.FromResult<object?>(SchemaVersion);
            }

            return Task.FromResult<object?>(null);
        }

        public Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(SqlStatement stmt)
        {
            ExecutedQuerySql.Add(stmt.Text);
            return Task.FromResult<IReadOnlyList<Dictionary<string, object>>>(Array.Empty<Dictionary<string, object>>());
        }

        public Task OpenAsync() => Task.CompletedTask;
        public Task CloseAsync() => Task.CompletedTask;

        private static bool TryGetVersionValue(SqlStatement stmt, out int version)
        {
            if (stmt.Parameters is not null)
            {
                foreach (var kvp in stmt.Parameters)
                {
                    var key = kvp.Key.TrimStart('@').ToLowerInvariant();
                    if (key is "0" or "version")
                    {
                        if (TryConvertToInt(kvp.Value, out version))
                            return true;
                    }
                }
            }

            var normalized = NormalizeSql(stmt.Text);
            var m = Regex.Match(normalized, "values\\\\s*\\\\(\\\\s*1\\\\s*,\\\\s*(\\\\d+)\\\\s*\\\\)");
            if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out version))
                return true;

            version = 0;
            return false;
        }

        private static bool TryConvertToInt(object? value, out int result)
        {
            if (value is null)
            {
                result = 0;
                return false;
            }

            switch (value)
            {
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = checked((int)l);
                    return true;
                case short s:
                    result = s;
                    return true;
                case byte b:
                    result = b;
                    return true;
                case string str when int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    result = parsed;
                    return true;
                default:
                    try
                    {
                        result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch
                    {
                        result = 0;
                        return false;
                    }
            }
        }
    }
}
