using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Domain.Entities;
using Game.Core.Ports;
using Game.Core.Repositories;

namespace Game.Godot.Adapters.Db;

public class SaveGameRepository : ISaveGameRepository
{
    private readonly ISqlDatabase _db;
    public SaveGameRepository(ISqlDatabase db) => _db = db;

    public Task UpsertAsync(SaveGame save)
    {
        // Use deterministic id to guarantee stable upsert semantics per (userId,slot)
        // This avoids creating multiple rows for same logical save when called repeatedly.
        if (string.IsNullOrEmpty(save.Id)) save.Id = $"{save.UserId}:{save.SlotNumber}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (save.CreatedAt == 0) save.CreatedAt = now;
        save.UpdatedAt = now;

        // Test-only: allow simulating an error to verify rollback behavior in tests
        if ((System.Environment.GetEnvironmentVariable("DB_SIMULATE_SAVE_UPSERT_ERROR") ?? "0") == "1")
            throw new InvalidOperationException("Simulated SaveGame upsert error");

        _db.Execute(SqlStatement.Positional(
            "INSERT INTO saves(id,user_id,slot_number,data,created_at,updated_at) VALUES(@0,@1,@2,@3,@4,@5) " +
            "ON CONFLICT(id) DO UPDATE SET user_id=@1, slot_number=@2, data=@3, updated_at=@5;",
            save.Id,
            save.UserId,
            save.SlotNumber,
            save.Data,
            save.CreatedAt,
            save.UpdatedAt));
        return Task.CompletedTask;
    }

    public Task<SaveGame?> GetAsync(string userId, int slot)
    {
        var rows = _db.Query(SqlStatement.Positional(
            "SELECT id,user_id,slot_number,data,created_at,updated_at FROM saves WHERE user_id=@0 AND slot_number=@1 LIMIT 1;",
            userId,
            slot));
        if (rows.Count == 0) return Task.FromResult<SaveGame?>(null);
        var row = rows[0];
        var saveGame = new SaveGame
        {
            Id = row["id"]?.ToString() ?? string.Empty,
            UserId = row["user_id"]?.ToString() ?? string.Empty,
            SlotNumber = Convert.ToInt32(row["slot_number"] ?? 0),
            Data = row["data"]?.ToString() ?? "",
            CreatedAt = Convert.ToInt64(row["created_at"] ?? 0),
            UpdatedAt = Convert.ToInt64(row["updated_at"] ?? 0)
        };
        return Task.FromResult<SaveGame?>(saveGame);
    }

    public Task<List<SaveGame>> ListByUserAsync(string userId)
    {
        var rows = _db.Query(SqlStatement.Positional(
            "SELECT id,user_id,slot_number,data,created_at,updated_at FROM saves WHERE user_id=@0 ORDER BY slot_number;",
            userId));
        var list = new List<SaveGame>(rows.Count);
        foreach (var row in rows)
        {
            list.Add(new SaveGame
            {
                Id = row["id"]?.ToString() ?? string.Empty,
                UserId = row["user_id"]?.ToString() ?? string.Empty,
                SlotNumber = Convert.ToInt32(row["slot_number"] ?? 0),
                Data = row["data"]?.ToString() ?? "",
                CreatedAt = Convert.ToInt64(row["created_at"] ?? 0),
                UpdatedAt = Convert.ToInt64(row["updated_at"] ?? 0)
            });
        }
        return Task.FromResult(list);
    }
}
