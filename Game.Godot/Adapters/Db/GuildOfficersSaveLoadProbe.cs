using System;
using System.Threading;
using Game.Core.Domain;
using Game.Core.Repositories;
using Godot;

namespace Game.Godot.Adapters.Db;

public partial class GuildOfficersSaveLoadProbe : Node
{
    public global::Godot.Collections.Dictionary RunOfficerSaveLoad(
        string dbPath,
        string guildId,
        string officerUserId,
        int slotValue)
    {
        var result = new global::Godot.Collections.Dictionary
        {
            ["assigned"] = false,
            ["loaded"] = false,
            ["persisted"] = false,
            ["error"] = string.Empty,
        };

        GodotSQLiteDatabase? db = null;
        GodotSQLiteDatabase? dbReload = null;

        try
        {
            var safe = SafeResourcePath.FromString(dbPath) ?? throw new NotSupportedException("Invalid database path.");
            var slot = (OfficerSlot)slotValue;

            db = new GodotSQLiteDatabase(safe);
            var repo = new SQLiteGuildRepository(db);

            var guild = new Guild(guildId, "OfficerTest", DateTimeOffset.UtcNow);
            guild.AddMember(new GuildMember(officerUserId, "Officer", GuildRole.Member));
            result["assigned"] = guild.AssignOfficer(slot, officerUserId);

            repo.SaveAsync(guild, CancellationToken.None).GetAwaiter().GetResult();
            db.CloseAsync().GetAwaiter().GetResult();

            dbReload = new GodotSQLiteDatabase(safe);
            var repoReload = new SQLiteGuildRepository(dbReload);
            var loaded = repoReload.GetByIdAsync(guildId, CancellationToken.None).GetAwaiter().GetResult();

            result["loaded"] = loaded != null;
            if (loaded != null)
            {
                result["persisted"] = loaded.GetOfficerAssignment(slot) == officerUserId;
            }
        }
        catch (Exception ex)
        {
            result["error"] = ex.GetType().Name;
        }
        finally
        {
            try { db?.CloseAsync().GetAwaiter().GetResult(); } catch { }
            try { dbReload?.CloseAsync().GetAwaiter().GetResult(); } catch { }
        }

        return result;
    }
}
