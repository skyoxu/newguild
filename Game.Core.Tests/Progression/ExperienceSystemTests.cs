using System.Collections.Generic;
using FluentAssertions;
using Game.Core.Progression;
using Xunit;

namespace Game.Core.Tests.Progression;

public class ExperienceSystemTests
{
    // ACC:T37.1 ACC:T49.1
    [Fact]
    public void Should_Calculate_Level_From_TotalXp_Using_Deterministic_Curve()
    {
        var system = new ExperienceSystem();
        var grants = new[]
        {
            new RewardGrant(
                grantId: "grant-media-1",
                guildId: "guild-1",
                sourceType: "core.media.beat.triggered",
                sourceId: "beat-1",
                rewards: new Dictionary<string, int>
                {
                    [RewardTypes.Experience] = 60
                }),
            new RewardGrant(
                grantId: "grant-raid-1",
                guildId: "guild-1",
                sourceType: "core.raid.resolved",
                sourceId: "raid-1",
                rewards: new Dictionary<string, int>
                {
                    [RewardTypes.Experience] = 60
                })
        };

        var snapshot = system.ApplyRewards(grants);

        snapshot.TotalXp.Should().Be(120);
        snapshot.Level.Should().Be(2);
        snapshot.NextLevelXp.Should().Be(200);
    }

    // ACC:T37.2
    [Fact]
    public void Should_Align_Xp_With_RewardLedger_Total_And_Ignore_ZeroPoint_Grants()
    {
        var system = new ExperienceSystem();
        var grants = new[]
        {
            new RewardGrant(
                grantId: "grant-media-2",
                guildId: "guild-1",
                sourceType: "core.media.beat.triggered",
                sourceId: "beat-2",
                rewards: new Dictionary<string, int>
                {
                    [RewardTypes.Experience] = 0
                }),
            new RewardGrant(
                grantId: "grant-raid-2",
                guildId: "guild-1",
                sourceType: "core.raid.resolved",
                sourceId: "raid-2",
                rewards: new Dictionary<string, int>
                {
                    [RewardTypes.Experience] = 20
                }),
            new RewardGrant(
                grantId: "grant-recruit-1",
                guildId: "guild-1",
                sourceType: "core.recruitment.offer.resolved",
                sourceId: "offer-1",
                rewards: new Dictionary<string, int>
                {
                    [RewardTypes.Experience] = 30
                })
        };

        var snapshot = system.ApplyRewards(grants);

        snapshot.TotalXp.Should().Be(50);
        snapshot.Level.Should().Be(1);
    }

    // ACC:T37.3
    [Fact]
    public void Should_Restore_State_From_Snapshot_Without_Loss()
    {
        var system = new ExperienceSystem();
        var saved = new ExperienceSnapshot(250, 3, 300);

        system.Restore(saved);
        var snapshot = system.Snapshot();

        snapshot.Should().Be(saved);
    }
}
