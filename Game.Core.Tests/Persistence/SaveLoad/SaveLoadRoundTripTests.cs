using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Persistence;
using Game.Core.Domain;
using Game.Core.Domain.ValueObjects;
using Game.Core.Ports;
using Game.Core.State;
using Xunit;

namespace Game.Core.Tests.Persistence.SaveLoad;

internal sealed class InMemoryDataStore : IDataStore
{
    private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);

    public Task SaveAsync(string key, string json)
    {
        _data[key] = json;
        return Task.CompletedTask;
    }

    public Task<string?> LoadAsync(string key)
    {
        _data.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task DeleteAsync(string key)
    {
        _data.Remove(key);
        return Task.CompletedTask;
    }
}

public sealed class SaveLoadRoundTripTests
{
    private static GameState CreateState(string id, int level, int score)
        => new(
            Id: id,
            Level: level,
            Score: score,
            Health: 100,
            Inventory: new[] { "item_sword", "item_potion" },
            Position: new Position(1.25, -9.5),
            Timestamp: new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc)
        );

    private static GameConfig CreateConfig()
        => new(
            MaxLevel: 50,
            InitialHealth: 100,
            ScoreMultiplier: 1.0,
            AutoSave: false,
            Difficulty: Difficulty.Medium
        );

    // ACC:T25.1
    [Fact]
    public async Task Should_Save_And_Load_RoundTrip_State_And_Config()
    {
        var store = new InMemoryDataStore();
        var saver = new GameStateManager(store, new GameStateManagerOptions(EnableCompression: false));

        var state = CreateState(id: "state-1", level: 7, score: 42);
        var config = CreateConfig();
        saver.SetState(state, config);

        var saveId = await saver.SaveGameAsync(name: "slot-1");

        var loader = new GameStateManager(store, new GameStateManagerOptions(EnableCompression: false));
        var (loadedState, loadedConfig) = await loader.LoadGameAsync(saveId);

        loadedState.Should().BeEquivalentTo(state);
        loadedConfig.Should().BeEquivalentTo(config);
    }

    // ACC:T25.1
    [Fact]
    public async Task Should_Emit_Save_And_Load_Events_With_Typed_Data_And_Stable_Order()
    {
        var store = new InMemoryDataStore();
        var mgr = new GameStateManager(store, new GameStateManagerOptions(EnableCompression: false));

        var events = new List<DomainEvent>();
        mgr.OnEvent(events.Add);

        mgr.SetState(CreateState(id: "state-2", level: 2, score: 10), CreateConfig());

        var saveId = await mgr.SaveGameAsync(name: "slot-2");
        await mgr.LoadGameAsync(saveId);

        var coreTypes = events
            .Select(e => e.Type)
            .Where(t => t.StartsWith("core.", StringComparison.Ordinal))
            .ToList();

        coreTypes.Should().ContainInOrder(
            SaveRequested.EventType,
            SaveCompleted.EventType,
            LoadRequested.EventType,
            LoadCompleted.EventType
        );

        var saveRequested = events.Single(e => e.Type == SaveRequested.EventType)
            .Data.Should().BeOfType<SaveRequested>().Subject;
        saveRequested.SaveId.Should().Be(saveId);

        var saveCompleted = events.Single(e => e.Type == SaveCompleted.EventType)
            .Data.Should().BeOfType<SaveCompleted>().Subject;
        saveCompleted.SaveId.Should().Be(saveId);

        var loadRequested = events.Single(e => e.Type == LoadRequested.EventType)
            .Data.Should().BeOfType<LoadRequested>().Subject;
        loadRequested.SaveId.Should().Be(saveId);

        var loadCompleted = events.Single(e => e.Type == LoadCompleted.EventType)
            .Data.Should().BeOfType<LoadCompleted>().Subject;
        loadCompleted.SaveId.Should().Be(saveId);
    }

    // ACC:T25.1
    [Fact]
    public async Task Should_Publish_LoadFailed_And_Not_Mutate_State_When_Checksum_Mismatch()
    {
        var store = new InMemoryDataStore();

        var saver = new GameStateManager(store, new GameStateManagerOptions(EnableCompression: false));
        saver.SetState(CreateState(id: "state-3", level: 3, score: 30), CreateConfig());
        var saveId = await saver.SaveGameAsync(name: "slot-3");

        var raw = await store.LoadAsync(saveId);
        raw.Should().NotBeNull();

        var saved = JsonSerializer.Deserialize<SaveData>(raw!);
        saved.Should().NotBeNull();

        var corruptedState = saved!.State with { Level = saved.State.Level + 1 };
        var corrupted = saved with { State = corruptedState };
        await store.SaveAsync(saveId, JsonSerializer.Serialize(corrupted));

        var loader = new GameStateManager(store, new GameStateManagerOptions(EnableCompression: false));
        var initialState = CreateState(id: "initial", level: 99, score: 999);
        var initialConfig = CreateConfig();
        loader.SetState(initialState, initialConfig);

        var events = new List<DomainEvent>();
        loader.OnEvent(events.Add);

        var act = async () => await loader.LoadGameAsync(saveId);
        await act.Should().ThrowAsync<InvalidOperationException>();

        loader.GetState().Should().BeEquivalentTo(initialState);
        loader.GetConfig().Should().BeEquivalentTo(initialConfig);

        var types = events.Select(e => e.Type).ToList();
        types.Should().Contain(LoadRequested.EventType);
        types.Should().Contain(LoadFailed.EventType);
        types.Should().NotContain(LoadCompleted.EventType);
    }

    // ACC:T25.1
    [Fact]
    public async Task Should_Migrate_Old_Save_Format_On_Load_And_Publish_Migration_Event()
    {
        var store = new InMemoryDataStore();

        var saver = new GameStateManager(store, new GameStateManagerOptions(EnableCompression: false));
        saver.SetState(CreateState(id: "state-4", level: 4, score: 40), CreateConfig());
        var saveId = await saver.SaveGameAsync(name: "slot-4");

        var raw = await store.LoadAsync(saveId);
        raw.Should().NotBeNull();

        var saved = JsonSerializer.Deserialize<SaveData>(raw!);
        saved.Should().NotBeNull();

        var currentVersion = saved!.Metadata.Version;
        var oldVersion = currentVersion == "0.0.0" ? "0.0.1" : "0.0.0";

        var downgraded = saved with
        {
            Metadata = new SaveMetadata(
                CreatedAt: saved.Metadata.CreatedAt,
                UpdatedAt: saved.Metadata.UpdatedAt,
                Version: oldVersion,
                Checksum: saved.Metadata.Checksum
            )
        };

        await store.SaveAsync(saveId, JsonSerializer.Serialize(downgraded));

        var loader = new GameStateManager(store, new GameStateManagerOptions(EnableCompression: false));
        var events = new List<DomainEvent>();
        loader.OnEvent(events.Add);

        await loader.LoadGameAsync(saveId);

        var coreTypes = events
            .Select(e => e.Type)
            .Where(t => t.StartsWith("core.", StringComparison.Ordinal))
            .ToList();

        coreTypes.Should().ContainInOrder(
            LoadRequested.EventType,
            SaveFormatMigrationApplied.EventType,
            LoadCompleted.EventType
        );

        var migratedRaw = await store.LoadAsync(saveId);
        migratedRaw.Should().NotBeNull();

        var migrated = JsonSerializer.Deserialize<SaveData>(migratedRaw!);
        migrated.Should().NotBeNull();
        migrated!.Metadata.Version.Should().Be(currentVersion);
    }
}
