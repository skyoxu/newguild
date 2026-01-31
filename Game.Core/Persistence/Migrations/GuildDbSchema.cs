using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Ports;

namespace Game.Core.Persistence.Migrations;

/// <summary>
/// Single source of truth for the Guild DB schema_version and migrations.
/// This schema is shared by all repositories that operate on the same Guild SQLite file.
/// </summary>
public static class GuildDbSchema
{
    /// <summary>
    /// Latest schema_version for the Guild DB file.
    /// Version strategy:
    /// - Bump whenever table structures or column semantics change.
    /// - Add deterministic, idempotent migration steps for each version.
    /// </summary>
    public const int LatestVersion = 3;

    private const string CreateGuildOfficersTableSql =
        "CREATE TABLE IF NOT EXISTS GuildOfficers (" +
        " GuildId TEXT NOT NULL," +
        " Slot INTEGER NOT NULL," +
        " UserId TEXT NOT NULL," +
        " PRIMARY KEY (GuildId, Slot)," +
        " FOREIGN KEY (GuildId) REFERENCES Guilds(GuildId) ON DELETE CASCADE" +
        " )";

    public static IReadOnlyDictionary<int, Func<ISQLiteDatabase, Task>> CreateMigrations()
    {
        return new Dictionary<int, Func<ISQLiteDatabase, Task>>
        {
            [1] = async database =>
            {
                // Guilds table
                await database.ExecuteNonQueryAsync(SqlStatement.NoParameters(
                    "CREATE TABLE IF NOT EXISTS Guilds (" +
                    " GuildId TEXT PRIMARY KEY," +
                    " CreatorId TEXT NOT NULL," +
                    " Name TEXT NOT NULL," +
                    " CreatedAt TEXT NOT NULL" +
                    " )"));

                // GuildMembers table
                await database.ExecuteNonQueryAsync(SqlStatement.NoParameters(
                    "CREATE TABLE IF NOT EXISTS GuildMembers (" +
                    " GuildId TEXT NOT NULL," +
                    " UserId TEXT NOT NULL," +
                    " Role INTEGER NOT NULL," +
                    " PRIMARY KEY (GuildId, UserId)," +
                    " FOREIGN KEY (GuildId) REFERENCES Guilds(GuildId) ON DELETE CASCADE" +
                    " )"));
            },
            [2] = async database =>
            {
                // Recruitment offers (pending only).
                await database.ExecuteNonQueryAsync(SqlStatement.NoParameters(
                    "CREATE TABLE IF NOT EXISTS RecruitmentOffers (" +
                    " OfferId TEXT PRIMARY KEY," +
                    " GuildId TEXT NOT NULL," +
                    " CandidateId TEXT NOT NULL," +
                    " Role INTEGER NOT NULL," +
                    " PresentedAt TEXT NOT NULL," +
                    " UNIQUE (GuildId, CandidateId)," +
                    " FOREIGN KEY (GuildId) REFERENCES Guilds(GuildId) ON DELETE CASCADE" +
                    " )"));
            },
            [3] = async database =>
            {
                // Officer assignments (one per slot).
                await database.ExecuteNonQueryAsync(SqlStatement.NoParameters(CreateGuildOfficersTableSql));
            },
        };
    }

    /// <summary>
    /// Stop-loss repair: ensure required tables exist even if schema_version is already at latest.
    /// This is idempotent (CREATE TABLE IF NOT EXISTS) and does not change schema_version.
    /// </summary>
    public static async Task EnsureTablesExistAsync(ISQLiteDatabase database)
    {
        if (database is null) throw new ArgumentNullException(nameof(database));

        // Version 1 tables
        await database.ExecuteNonQueryAsync(SqlStatement.NoParameters(
            "CREATE TABLE IF NOT EXISTS Guilds (" +
            " GuildId TEXT PRIMARY KEY," +
            " CreatorId TEXT NOT NULL," +
            " Name TEXT NOT NULL," +
            " CreatedAt TEXT NOT NULL" +
            " )")).ConfigureAwait(false);

        await database.ExecuteNonQueryAsync(SqlStatement.NoParameters(
            "CREATE TABLE IF NOT EXISTS GuildMembers (" +
            " GuildId TEXT NOT NULL," +
            " UserId TEXT NOT NULL," +
            " Role INTEGER NOT NULL," +
            " PRIMARY KEY (GuildId, UserId)," +
            " FOREIGN KEY (GuildId) REFERENCES Guilds(GuildId) ON DELETE CASCADE" +
            " )")).ConfigureAwait(false);

        // Version 2 tables
        await database.ExecuteNonQueryAsync(SqlStatement.NoParameters(
            "CREATE TABLE IF NOT EXISTS RecruitmentOffers (" +
            " OfferId TEXT PRIMARY KEY," +
            " GuildId TEXT NOT NULL," +
            " CandidateId TEXT NOT NULL," +
            " Role INTEGER NOT NULL," +
            " PresentedAt TEXT NOT NULL," +
            " UNIQUE (GuildId, CandidateId)," +
            " FOREIGN KEY (GuildId) REFERENCES Guilds(GuildId) ON DELETE CASCADE" +
            " )")).ConfigureAwait(false);

        // Version 3 tables
        await database.ExecuteNonQueryAsync(SqlStatement.NoParameters(CreateGuildOfficersTableSql)).ConfigureAwait(false);
    }
}

