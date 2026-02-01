using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.State;
using Xunit;

namespace Game.Core.Tests.Persistence.SaveLoad;

public sealed class WorldGenSaveLoadReplayTests
{
    private static GameConfig CreateConfig()
        => new(
            MaxLevel: 10,
            InitialHealth: 100,
            ScoreMultiplier: 1.0,
            AutoSave: false,
            Difficulty: Difficulty.Medium
        );

    // ACC:T40.7
    [Fact]
    public async Task Should_Save_And_Load_Worldgen_Output_Deterministically_For_Same_Seed()
    {
        const string seed = "seed-replay-0001";

        var store = new InMemoryDataStore();
        var saver = new GameStateManager(store, new GameStateManagerOptions(EnableCompression: false));

        var config = CreateConfig();
        var inventory = new Inventory();
        var engine = new GameEngineCore(config, inventory, seed: seed, bus: null);

        var startedState = engine.Start();
        startedState.Seed.Should().Be(seed);
        startedState.NpcGuildIds.Should().NotBeNull();
        startedState.NpcGuildIds.Should().HaveCount(5);
        startedState.NpcGuildIds.Distinct().Count().Should().Be(5);

        saver.SetState(startedState, config);
        var saveId = await saver.SaveGameAsync(name: "worldgen-seed-replay");

        var loader = new GameStateManager(store, new GameStateManagerOptions(EnableCompression: false));
        var (loadedState, loadedConfig) = await loader.LoadGameAsync(saveId);

        loadedConfig.Should().BeEquivalentTo(config);
        loadedState.Seed.Should().Be(seed);
        loadedState.NpcGuildIds.Should().Equal(startedState.NpcGuildIds);

        var secondEngine = new GameEngineCore(config, new Inventory(), seed: seed, bus: null);
        var secondStartedState = secondEngine.Start();
        secondStartedState.NpcGuildIds.Should().Equal(loadedState.NpcGuildIds);
    }
}
