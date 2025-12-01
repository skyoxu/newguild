using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Game.Core.Domain;
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
    private readonly ISQLiteDatabase _db;
    private bool _initialized;

    public SQLiteGuildRepository(ISQLiteDatabase database)
    {
        _db = database ?? throw new ArgumentNullException(nameof(database));
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _db.OpenAsync();

        // Create Guilds table
        await _db.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS Guilds (
                GuildId TEXT PRIMARY KEY,
                CreatorId TEXT NOT NULL,
                Name TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            )
        ");

        // Create GuildMembers table
        await _db.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS GuildMembers (
                GuildId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                Role INTEGER NOT NULL,
                PRIMARY KEY (GuildId, UserId),
                FOREIGN KEY (GuildId) REFERENCES Guilds(GuildId) ON DELETE CASCADE
            )
        ");

        _initialized = true;
    }

    public async Task<Guild> CreateAsync(Guild guild)
    {
        await EnsureInitializedAsync();

        // Insert guild
        await _db.ExecuteNonQueryAsync(
            "INSERT INTO Guilds (GuildId, CreatorId, Name, CreatedAt) VALUES (@GuildId, @CreatorId, @Name, @CreatedAt)",
            new Dictionary<string, object>
            {
                ["@GuildId"] = guild.GuildId,
                ["@CreatorId"] = guild.CreatorId,
                ["@Name"] = guild.Name,
                ["@CreatedAt"] = guild.CreatedAt.ToString("O") // ISO 8601 format
            }
        );

        // Insert members
        foreach (var member in guild.Members)
        {
            await _db.ExecuteNonQueryAsync(
                "INSERT INTO GuildMembers (GuildId, UserId, Role) VALUES (@GuildId, @UserId, @Role)",
                new Dictionary<string, object>
                {
                    ["@GuildId"] = guild.GuildId,
                    ["@UserId"] = member.UserId,
                    ["@Role"] = (int)member.Role
                }
            );
        }

        return guild;
    }

    public async Task<Guild?> GetByIdAsync(string guildId)
    {
        await EnsureInitializedAsync();

        var rows = await _db.QueryAsync(
            "SELECT GuildId, CreatorId, Name, CreatedAt FROM Guilds WHERE GuildId = @GuildId",
            new Dictionary<string, object> { ["@GuildId"] = guildId }
        );

        if (rows.Count == 0)
            return null;

        return await ReconstructGuildAsync(rows[0]);
    }

    public async Task<Guild> UpdateAsync(Guild guild)
    {
        await EnsureInitializedAsync();

        // Update guild
        await _db.ExecuteNonQueryAsync(
            "UPDATE Guilds SET Name = @Name WHERE GuildId = @GuildId",
            new Dictionary<string, object>
            {
                ["@GuildId"] = guild.GuildId,
                ["@Name"] = guild.Name
            }
        );

        // Delete existing members
        await _db.ExecuteNonQueryAsync(
            "DELETE FROM GuildMembers WHERE GuildId = @GuildId",
            new Dictionary<string, object> { ["@GuildId"] = guild.GuildId }
        );

        // Insert updated members
        foreach (var member in guild.Members)
        {
            await _db.ExecuteNonQueryAsync(
                "INSERT INTO GuildMembers (GuildId, UserId, Role) VALUES (@GuildId, @UserId, @Role)",
                new Dictionary<string, object>
                {
                    ["@GuildId"] = guild.GuildId,
                    ["@UserId"] = member.UserId,
                    ["@Role"] = (int)member.Role
                }
            );
        }

        return guild;
    }

    public async Task<bool> DeleteAsync(string guildId)
    {
        await EnsureInitializedAsync();

        var affected = await _db.ExecuteNonQueryAsync(
            "DELETE FROM Guilds WHERE GuildId = @GuildId",
            new Dictionary<string, object> { ["@GuildId"] = guildId }
        );

        // CASCADE will delete members automatically
        return affected > 0;
    }

    public async Task<IReadOnlyList<Guild>> GetAllAsync()
    {
        await EnsureInitializedAsync();

        var rows = await _db.QueryAsync("SELECT GuildId, CreatorId, Name, CreatedAt FROM Guilds");

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

        var rows = await _db.QueryAsync(@"
            SELECT DISTINCT g.GuildId, g.CreatorId, g.Name, g.CreatedAt
            FROM Guilds g
            INNER JOIN GuildMembers gm ON g.GuildId = gm.GuildId
            WHERE gm.UserId = @UserId
        ", new Dictionary<string, object> { ["@UserId"] = userId });

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
        var memberRows = await _db.QueryAsync(
            "SELECT UserId, Role FROM GuildMembers WHERE GuildId = @GuildId",
            new Dictionary<string, object> { ["@GuildId"] = guildId }
        );

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

        // Use static factory method instead of reflection
        return Guild.ReconstructFromDatabase(guildId, creatorId, name, createdAt, members);
    }
}
