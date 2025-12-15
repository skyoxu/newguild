using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Game.Core.Contracts;
using Game.Core.Domain;
using Game.Core.Ports;
using Game.Core.State;
using Xunit;

namespace Game.Core.Tests.State;

internal sealed class InMemoryDataStore : IDataStore
{
    private readonly Dictionary<string,string> _dict = new();
    public Task SaveAsync(string key, string json) { _dict[key] = json; return Task.CompletedTask; }
    public Task<string?> LoadAsync(string key) { _dict.TryGetValue(key, out var v); return Task.FromResult(v); }
    public Task DeleteAsync(string key) { _dict.Remove(key); return Task.CompletedTask; }
    public IReadOnlyDictionary<string,string> Snapshot => _dict;
}

internal sealed class CapturingLogger : ILogger
{
    public int WarnCalls { get; private set; }
    public int ErrorCalls { get; private set; }

    public void Info(string message) { }
    public void Warn(string message) => WarnCalls++;
    public void Error(string message) => ErrorCalls++;
    public void Error(string message, Exception ex) => ErrorCalls++;
}

public class GameStateManagerTests
{
    private static GameState MakeState(int level=1, int score=0)
        => new(
            Id: Guid.NewGuid().ToString(),
            Level: level,
            Score: score,
            Health: 100,
            Inventory: Array.Empty<string>(),
            Position: new Game.Core.Domain.ValueObjects.Position(0,0),
            Timestamp: DateTime.UtcNow
        );

    private static GameConfig MakeConfig()
        => new(
            MaxLevel: 50,
            InitialHealth: 100,
            ScoreMultiplier: 1.0,
            AutoSave: false,
            Difficulty: Difficulty.Medium
        );

    [Fact]
    public async Task Save_load_delete_and_index_flow_works_with_compression()
    {
        var store = new InMemoryDataStore();
        var opts = new GameStateManagerOptions(MaxSaves: 2, EnableCompression: true);
        var mgr = new GameStateManager(store, opts);

        var seen = new List<string>();
        mgr.OnEvent(e => seen.Add(e.Type));

        mgr.SetState(MakeState(level:2), MakeConfig());
        var id1 = await mgr.SaveGameAsync("slot1");
        Assert.Contains("game.save.created", seen);
        Assert.True(store.Snapshot.ContainsKey(id1));
        Assert.StartsWith("gz:", store.Snapshot[id1]);

        mgr.SetState(MakeState(level:3), MakeConfig());
        var id2 = await mgr.SaveGameAsync("slot2");
        var list = await mgr.GetSaveListAsync();
        Assert.True(list.Count >= 2);

        // Trigger cleanup by saving third; MaxSaves=2 => first gets deleted from store
        mgr.SetState(MakeState(level:4), MakeConfig());
        var id3 = await mgr.SaveGameAsync("slot3");

        var saveIndexKey = opts.StorageKey + ":index";
        var indexJson = await store.LoadAsync(saveIndexKey);
        Assert.NotNull(indexJson);
        var ids = JsonSerializer.Deserialize<List<string>>(indexJson!)!;
        Assert.Equal(2, ids.Count);
        Assert.DoesNotContain(id1, ids);

        // load latest
        var (state, cfg) = await mgr.LoadGameAsync(id3);
        Assert.Equal(4, state.Level);
        Assert.Equal(100, cfg.InitialHealth);

        // delete second
        await mgr.DeleteSaveAsync(id2);
        indexJson = await store.LoadAsync(saveIndexKey);
        ids = JsonSerializer.Deserialize<List<string>>(indexJson!)!;
        Assert.DoesNotContain(id2, ids);
    }

    [Fact]
    public async Task AutoSave_toggle_and_tick()
    {
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store);
        mgr.SetState(MakeState(level:5), MakeConfig());
        mgr.EnableAutoSave();
        await mgr.AutoSaveTickAsync();
        mgr.DisableAutoSave();
        var idx = await store.LoadAsync("guild-manager-game:index");
        Assert.NotNull(idx);
    }

    [Fact]
    public async Task Save_throws_when_state_missing_or_title_too_long()
    {
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await mgr.SaveGameAsync());

        mgr.SetState(MakeState(), MakeConfig());
        var tooLong = new string('x', 101);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await mgr.SaveGameAsync(tooLong));
    }

    [Fact]
    public async Task Save_throws_when_screenshot_too_large()
    {
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store);
        mgr.SetState(MakeState(), MakeConfig());

        var tooLargeScreenshot = new string('x', 2_000_001);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await mgr.SaveGameAsync(screenshot: tooLargeScreenshot));
    }

    [Fact]
    public void GetState_returns_null_when_unset()
    {
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store);
        Assert.Null(mgr.GetState());
        Assert.Null(mgr.GetConfig());
    }

    [Fact]
    public async Task LoadGameAsync_throws_when_save_not_found()
    {
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await mgr.LoadGameAsync("missing-save-id"));
    }

    [Fact]
    public async Task LoadGameAsync_throws_when_checksum_mismatch()
    {
        var store = new InMemoryDataStore();

        var saveId = "bad-checksum-save";
        var state = MakeState(level: 2, score: 10);
        var cfg = MakeConfig();
        var save = new SaveData(
            Id: saveId,
            State: state,
            Config: cfg,
            Metadata: new SaveMetadata(DateTime.UtcNow, DateTime.UtcNow, "1.0.0", "BAD")
        );
        await store.SaveAsync(saveId, JsonSerializer.Serialize(save));

        var mgr = new GameStateManager(store);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await mgr.LoadGameAsync(saveId));
    }

    [Fact]
    public async Task GetSaveListAsync_skips_broken_saves_and_logs_warning()
    {
        var store = new InMemoryDataStore();
        var logger = new CapturingLogger();
        var mgr = new GameStateManager(store, logger: logger);

        mgr.SetState(MakeState(level: 1), MakeConfig());
        var goodId = await mgr.SaveGameAsync("good");

        mgr.SetState(MakeState(level: 2), MakeConfig());
        var badId = await mgr.SaveGameAsync("bad");

        // Corrupt the second save payload to trigger JSON deserialize failure.
        await store.SaveAsync(badId, "{");

        var list = await mgr.GetSaveListAsync();
        Assert.Contains(list, s => s.Id == goodId);
        Assert.DoesNotContain(list, s => s.Id == badId);
        Assert.True(logger.WarnCalls >= 1, "broken save should emit at least one warning");
    }

    [Fact]
    public async Task GetSaveListAsync_returns_empty_when_index_is_null_literal()
    {
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store);

        // Simulate a corrupted index file content that deserializes to null.
        await store.SaveAsync("guild-manager-game:index", "null");

        var list = await mgr.GetSaveListAsync();
        Assert.Empty(list);
    }

    [Fact]
    public void Publish_continues_with_remaining_callbacks_when_one_throws()
    {
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store);

        var callback1Called = false;
        var callback2Called = false;
        var callback3Called = false;

        // First callback: succeeds
        mgr.OnEvent(evt => callback1Called = true);

        // Second callback: throws exception
        mgr.OnEvent(evt => throw new InvalidOperationException("Callback failed"));

        // Third callback: should still be called despite second callback throwing
        mgr.OnEvent(evt => callback3Called = true);

        // Trigger event publishing by setting state (which calls Publish internally)
        mgr.SetState(MakeState(), MakeConfig());

        // All callbacks should have been attempted
        Assert.True(callback1Called);
        Assert.True(callback3Called);
        // callback2Called cannot be checked as it throws, but the test verifies callback3 ran
    }

    [Fact]
    public async Task SaveGameAsync_WithCompressionEnabled_CompressesData()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var opts = new GameStateManagerOptions(EnableCompression: true);
        var mgr = new GameStateManager(store, opts);
        mgr.SetState(MakeState(), MakeConfig());

        // Act
        var saveId = await mgr.SaveGameAsync("compressed-save");

        // Assert - compressed data should start with "gz:"
        var rawData = store.Snapshot[saveId];
        Assert.StartsWith("gz:", rawData);
    }

    [Fact]
    public async Task SaveGameAsync_WithCompressionDisabled_StoresPlainJson()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var opts = new GameStateManagerOptions(EnableCompression: false);
        var mgr = new GameStateManager(store, opts);
        mgr.SetState(MakeState(), MakeConfig());

        // Act
        var saveId = await mgr.SaveGameAsync("plain-save");

        // Assert - uncompressed data should be valid JSON
        var rawData = store.Snapshot[saveId];
        Assert.DoesNotContain("gz:", rawData);
        var parsed = JsonSerializer.Deserialize<JsonDocument>(rawData);
        Assert.NotNull(parsed);
    }

    [Fact]
    public async Task LoadGameAsync_WithCompressedData_DecompressesCorrectly()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var opts = new GameStateManagerOptions(EnableCompression: true);
        var mgr = new GameStateManager(store, opts);
        var originalState = MakeState(level: 5, score: 1000);
        mgr.SetState(originalState, MakeConfig());
        var saveId = await mgr.SaveGameAsync();

        // Act - load from compressed storage
        var mgr2 = new GameStateManager(store, opts);
        var (loadedState, _) = await mgr2.LoadGameAsync(saveId);

        // Assert - decompression worked
        Assert.Equal(5, loadedState.Level);
        Assert.Equal(1000, loadedState.Score);
    }

    [Fact]
    public void EnableAutoSave_CalledTwice_OnlyPublishesEventOnce()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store);
        var eventCount = 0;
        mgr.OnEvent(evt => { if (evt.Type == "game.autosave.enabled") eventCount++; });

        // Act - call EnableAutoSave twice
        mgr.EnableAutoSave();
        mgr.EnableAutoSave();

        // Assert - event published only once (early return on second call)
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void DisableAutoSave_CalledTwice_OnlyPublishesEventOnce()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store);
        mgr.EnableAutoSave();
        var eventCount = 0;
        mgr.OnEvent(evt => { if (evt.Type == "game.autosave.disabled") eventCount++; });

        // Act - call DisableAutoSave twice
        mgr.DisableAutoSave();
        mgr.DisableAutoSave();

        // Assert - event published only once (early return on second call)
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void SetState_WithNullConfig_DoesNotCrash()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store);

        // Act - call with null config (tests line 31 branch)
        mgr.SetState(MakeState(), config: null);

        // Assert - should not throw
        var retrievedState = mgr.GetState();
        Assert.NotNull(retrievedState);
        var retrievedConfig = mgr.GetConfig();
        Assert.Null(retrievedConfig);
    }

    [Fact]
    public async Task CleanupOldSavesAsync_WithNoIndex_ExitsEarly()
    {
        // Arrange - empty store with no index
        var store = new InMemoryDataStore();
        var opts = new GameStateManagerOptions(MaxSaves: 2);
        var mgr = new GameStateManager(store, opts);

        // Act - save a game (which triggers cleanup internally)
        mgr.SetState(MakeState(), MakeConfig());
        var saveId = await mgr.SaveGameAsync();

        // Assert - save succeeded even with no existing index (tests line 240 branch)
        var list = await mgr.GetSaveListAsync();
        Assert.Single(list);
    }

    [Fact]
    public void SetState_WithConfig_SetsConfig()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store);

        // Act - call with config (tests line 31-32 true branch)
        mgr.SetState(MakeState(), MakeConfig());

        // Assert
        var retrievedConfig = mgr.GetConfig();
        Assert.NotNull(retrievedConfig);
        Assert.Equal(50, retrievedConfig.MaxLevel);
    }

    [Fact]
    public async Task AutoSaveTickAsync_WhenDisabled_DoesNotSave()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store);
        mgr.SetState(MakeState(), MakeConfig());

        // Act - auto-save disabled by default, tick should do nothing
        await mgr.AutoSaveTickAsync();

        // Assert - no saves created (tests line 172 false branch)
        var list = await mgr.GetSaveListAsync();
        Assert.Empty(list);
    }
}
