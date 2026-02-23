using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Game.Core.Contracts.Achievements;
using Game.Core.Ports;

namespace Game.Godot.Adapters;

public sealed class AchievementStateStoreAdapter : IAchievementStateStore
{
    private const string KeyPrefix = "achievement_state";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IDataStore _dataStore;

    public AchievementStateStoreAdapter(IDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    public async Task<AchievementStateSnapshot?> LoadAsync(string saveId)
    {
        var key = BuildKey(saveId);
        var json = await _dataStore.LoadAsync(key);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var payload = JsonSerializer.Deserialize<PersistedState>(json, JsonOptions);
            if (payload == null)
                return null;

            if (!AchievementStateSnapshotMigration.TryMigrateToCurrent(
                    payload.SchemaVersion,
                    payload.UnlockedTriggerEventTypes,
                    out var snapshot))
            {
                return null;
            }

            return snapshot;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task SaveAsync(string saveId, AchievementStateSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        var key = BuildKey(saveId);
        if (!AchievementStateSnapshotMigration.TryMigrateToCurrent(
                snapshot.SchemaVersion,
                snapshot.UnlockedTriggerEventTypes,
                out var normalizedSnapshot))
        {
            throw new InvalidOperationException($"Unsupported achievement snapshot schemaVersion={snapshot.SchemaVersion}.");
        }

        var payload = new PersistedState
        {
            SchemaVersion = AchievementStateSnapshot.CurrentSchemaVersion,
            UnlockedCount = normalizedSnapshot.UnlockedCount,
            UnlockedTriggerEventTypes = new System.Collections.Generic.List<string>(normalizedSnapshot.UnlockedTriggerEventTypes),
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return _dataStore.SaveAsync(key, json);
    }

    private static string BuildKey(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("saveId cannot be null or whitespace.", nameof(saveId));

        return $"{KeyPrefix}_{saveId.Trim()}";
    }

    private sealed class PersistedState
    {
        public int SchemaVersion { get; set; }

        public int UnlockedCount { get; set; }

        public List<string> UnlockedTriggerEventTypes { get; set; } = new();
    }
}
