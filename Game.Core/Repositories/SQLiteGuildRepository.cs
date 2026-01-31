using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Domain;
using Game.Core.Persistence.Migrations;
using Game.Core.Ports;

namespace Game.Core.Repositories;

/// <summary>
/// SQLite implementation of IGuildRepository.
/// Follows ADR-0018 (pure C# implementation, zero Godot dependencies).
/// Uses ISQLiteDatabase port for database operations (ADR-0007).
/// Database schema follows ADR-0023 (storage separation).
/// </summary>
public class SQLiteGuildRepository : IGuildRepository
{
    /// <summary>
    /// Latest schema_version for the Guild DB file.
    /// The schema is shared by all repositories operating on the same DB (see GuildDbSchema).
    /// </summary>
    private const int LatestGuildSchemaVersion = GuildDbSchema.LatestVersion;

    private readonly ISQLiteDatabase _db;
    private bool _initialized;

    private static readonly SqlStatement BeginImmediateTransaction = SqlStatement.NoParameters("BEGIN IMMEDIATE");
    private static readonly SqlStatement CommitTransaction = SqlStatement.NoParameters("COMMIT");
    private static readonly SqlStatement RollbackTransaction = SqlStatement.NoParameters("ROLLBACK");

    public SQLiteGuildRepository(ISQLiteDatabase database)
    {
        _db = database ?? throw new ArgumentNullException(nameof(database));
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _db.OpenAsync().ConfigureAwait(false);
        await SchemaMigrationRunner.EnsureLatestAsync(_db, LatestGuildSchemaVersion, GuildDbSchema.CreateMigrations())
            .ConfigureAwait(false);
        await GuildDbSchema.EnsureTablesExistAsync(_db).ConfigureAwait(false);

        _initialized = true;
    }

    public async Task<Guild> CreateAsync(Guild guild)
    {
        await EnsureInitializedAsync();

        await _db.ExecuteNonQueryAsync(BeginImmediateTransaction).ConfigureAwait(false);
        try
        {
            // Insert guild
            await _db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
                "INSERT INTO Guilds (GuildId, CreatorId, Name, CreatedAt) VALUES (@GuildId, @CreatorId, @Name, @CreatedAt)",
                new Dictionary<string, object?>
                {
                    ["@GuildId"] = guild.GuildId,
                    ["@CreatorId"] = guild.CreatorId,
                    ["@Name"] = guild.Name,
                    ["@CreatedAt"] = guild.CreatedAt.ToString("O") // ISO 8601 format
                })).ConfigureAwait(false);

            // Insert members
            foreach (var member in guild.Members)
            {
                await _db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
                    "INSERT INTO GuildMembers (GuildId, UserId, Role) VALUES (@GuildId, @UserId, @Role)",
                    new Dictionary<string, object?>
                    {
                        ["@GuildId"] = guild.GuildId,
                        ["@UserId"] = member.UserId,
                        ["@Role"] = (int)member.Role
                    })).ConfigureAwait(false);
            }

            await PersistOfficerAssignmentsAsync(guild).ConfigureAwait(false);
            await _db.ExecuteNonQueryAsync(CommitTransaction).ConfigureAwait(false);

            return guild;
        }
        catch
        {
            try
            {
                await _db.ExecuteNonQueryAsync(RollbackTransaction).ConfigureAwait(false);
            }
            catch
            {
                // best-effort rollback
            }
            throw;
        }
    }

    public async Task<Guild?> GetByIdAsync(string guildId)
    {
        await EnsureInitializedAsync();

        var rows = await _db.QueryAsync(SqlStatement.WithParameters(
            "SELECT GuildId, CreatorId, Name, CreatedAt FROM Guilds WHERE GuildId = @GuildId",
            new Dictionary<string, object?> { ["@GuildId"] = guildId }));

        if (rows.Count == 0)
            return null;

        return await ReconstructGuildAsync(rows[0]);
    }

    public async Task<Guild?> GetByIdAsync(string guildId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await GetByIdAsync(guildId).ConfigureAwait(false);
    }

    public async Task SaveAsync(Guild guild, CancellationToken cancellationToken)
    {
        if (guild == null) throw new ArgumentNullException(nameof(guild));
        cancellationToken.ThrowIfCancellationRequested();

        await EnsureInitializedAsync();

        var existing = await GetByIdAsync(guild.GuildId).ConfigureAwait(false);
        if (existing == null)
        {
            await CreateAsync(guild).ConfigureAwait(false);
            return;
        }

        await UpdateAsync(guild).ConfigureAwait(false);
    }

    public async Task<Guild> UpdateAsync(Guild guild)
    {
        await EnsureInitializedAsync();

        await _db.ExecuteNonQueryAsync(BeginImmediateTransaction).ConfigureAwait(false);
        try
        {
            // Update guild
            await _db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
                "UPDATE Guilds SET Name = @Name WHERE GuildId = @GuildId",
                new Dictionary<string, object?>
                {
                    ["@GuildId"] = guild.GuildId,
                    ["@Name"] = guild.Name
                })).ConfigureAwait(false);

            // Delete existing members
            await _db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
                "DELETE FROM GuildMembers WHERE GuildId = @GuildId",
                new Dictionary<string, object?> { ["@GuildId"] = guild.GuildId })).ConfigureAwait(false);

            // Insert updated members
            foreach (var member in guild.Members)
            {
                await _db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
                    "INSERT INTO GuildMembers (GuildId, UserId, Role) VALUES (@GuildId, @UserId, @Role)",
                    new Dictionary<string, object?>
                    {
                        ["@GuildId"] = guild.GuildId,
                        ["@UserId"] = member.UserId,
                        ["@Role"] = (int)member.Role
                    })).ConfigureAwait(false);
            }

            await PersistOfficerAssignmentsAsync(guild).ConfigureAwait(false);
            await _db.ExecuteNonQueryAsync(CommitTransaction).ConfigureAwait(false);

            return guild;
        }
        catch
        {
            try
            {
                await _db.ExecuteNonQueryAsync(RollbackTransaction).ConfigureAwait(false);
            }
            catch
            {
                // best-effort rollback
            }
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string guildId)
    {
        await EnsureInitializedAsync();

        var affected = await _db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
            "DELETE FROM Guilds WHERE GuildId = @GuildId",
            new Dictionary<string, object?> { ["@GuildId"] = guildId }));

        // CASCADE will delete members automatically
        return affected > 0;
    }

    public async Task<IReadOnlyList<Guild>> GetAllAsync()
    {
        await EnsureInitializedAsync();

        var rows = await _db.QueryAsync(SqlStatement.NoParameters("SELECT GuildId, CreatorId, Name, CreatedAt FROM Guilds"));

        var guilds = new List<Guild>();
        foreach (var row in rows)
        {
            var guild = await ReconstructGuildAsync(row);
            guilds.Add(guild);
        }

        return guilds;
    }

    public async Task<IReadOnlyList<Guild>> FindByMemberAsync(string userId)
    {
        await EnsureInitializedAsync();

        var rows = await _db.QueryAsync(SqlStatement.WithParameters(@"
            SELECT DISTINCT g.GuildId, g.CreatorId, g.Name, g.CreatedAt
            FROM Guilds g
            INNER JOIN GuildMembers gm ON g.GuildId = gm.GuildId
            WHERE gm.UserId = @UserId
        ", new Dictionary<string, object?> { ["@UserId"] = userId }));

        var guilds = new List<Guild>();
        foreach (var row in rows)
        {
            var guild = await ReconstructGuildAsync(row);
            guilds.Add(guild);
        }

        return guilds;
    }

    private async Task<Guild> ReconstructGuildAsync(Dictionary<string, object> row)
    {
        var guildId = (string)row["GuildId"];
        var creatorId = (string)row["CreatorId"];
        var name = (string)row["Name"];
        var createdAtStr = (string)row["CreatedAt"];
        var createdAt = DateTimeOffset.Parse(createdAtStr);

        // Fetch members from database
        var memberRows = await _db.QueryAsync(SqlStatement.WithParameters(
            "SELECT UserId, Role FROM GuildMembers WHERE GuildId = @GuildId",
            new Dictionary<string, object?> { ["@GuildId"] = guildId }));

        // Build member list from database
        var members = new List<GuildMember>();
        foreach (var memberRow in memberRows)
        {
            var userId = (string)memberRow["UserId"];
            // Handle both int (mock) and long (real SQLite)
            var roleValue = memberRow["Role"];
            var role = roleValue is long longValue
                ? (GuildRole)(int)longValue
                : (GuildRole)(int)roleValue;
            members.Add(new GuildMember(userId, role));
        }

        var officerAssignments = await LoadOfficerAssignmentsAsync(guildId).ConfigureAwait(false);

        // Use static factory method instead of reflection
        var guild = Guild.ReconstructFromDatabase(guildId, creatorId, name, createdAt, members);
        if (officerAssignments.Count > 0)
        {
            guild.RestoreOfficerAssignments(officerAssignments);
        }

        return guild;
    }

    private async Task PersistOfficerAssignmentsAsync(Guild guild)
    {
        await _db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
            "DELETE FROM GuildOfficers WHERE GuildId = @GuildId",
            new Dictionary<string, object?> { ["@GuildId"] = guild.GuildId })).ConfigureAwait(false);

        foreach (var entry in guild.OfficerAssignments)
        {
            await _db.ExecuteNonQueryAsync(SqlStatement.WithParameters(
                "INSERT INTO GuildOfficers (GuildId, Slot, UserId) VALUES (@GuildId, @Slot, @UserId)",
                new Dictionary<string, object?>
                {
                    ["@GuildId"] = guild.GuildId,
                    ["@Slot"] = (int)entry.Key,
                    ["@UserId"] = entry.Value
                })).ConfigureAwait(false);
        }
    }

    private async Task<Dictionary<OfficerSlot, string>> LoadOfficerAssignmentsAsync(string guildId)
    {
        var assignments = new Dictionary<OfficerSlot, string>();
        var rows = await _db.QueryAsync(SqlStatement.WithParameters(
            "SELECT Slot, UserId FROM GuildOfficers WHERE GuildId = @GuildId",
            new Dictionary<string, object?> { ["@GuildId"] = guildId })).ConfigureAwait(false);

        foreach (var row in rows)
        {
            var slotValue = row["Slot"];
            var slot = slotValue is long longValue
                ? (OfficerSlot)(int)longValue
                : (OfficerSlot)(int)slotValue;
            var userId = (string)row["UserId"];
            assignments[slot] = userId;
        }

        return assignments;
    }
}
