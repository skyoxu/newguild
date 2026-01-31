using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Ports;
using Game.Core.Repositories;
using Xunit;

namespace Game.Core.Tests.Repositories;

public class FileSQLiteDatabaseTests
{
    [Fact]
    public async Task Should_Execute_Query_And_Scalar_After_Open()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"file-sqlite-{Guid.NewGuid():N}.db");
        var db = new FileSQLiteDatabase(dbPath);

        try
        {
            await db.OpenAsync();

            await db.ExecuteNonQueryAsync(SqlStatement.NoParameters(
                "CREATE TABLE IF NOT EXISTS Sample (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL)"));

            await db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
                "INSERT INTO Sample (Id, Name) VALUES (@Id, @Name)",
                new Dictionary<string, object?>
                {
                    ["@Id"] = 1,
                    ["@Name"] = "Alpha"
                }));

            var scalar = await db.ExecuteScalarAsync(SqlStatement.NoParameters(
                "SELECT COUNT(*) FROM Sample"));

            scalar.Should().Be(1);

            var rows = await db.QueryAsync(SqlStatement.NoParameters(
                "SELECT Id, Name FROM Sample"));

            rows.Should().HaveCount(1);
            rows[0]["Name"].Should().Be("Alpha");
        }
        finally
        {
            await db.CloseAsync();
            db.Dispose();
            TryDelete(dbPath);
        }
    }

    [Fact]
    public async Task Should_Allow_Open_And_Close_Multiple_Times()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"file-sqlite-{Guid.NewGuid():N}.db");
        var db = new FileSQLiteDatabase(dbPath);

        try
        {
            db.Dispose();

            await db.OpenAsync();
            await db.OpenAsync();

            await db.CloseAsync();
            await db.CloseAsync();

            db.Dispose();
        }
        finally
        {
            TryDelete(dbPath);
        }
    }

    [Fact]
    public async Task Should_Throw_When_Executing_Before_Open()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"file-sqlite-{Guid.NewGuid():N}.db");
        var db = new FileSQLiteDatabase(dbPath);

        Func<Task> act = async () => await db.ExecuteNonQueryAsync(SqlStatement.NoParameters(
            "CREATE TABLE IF NOT EXISTS Sample (Id INTEGER PRIMARY KEY)"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
