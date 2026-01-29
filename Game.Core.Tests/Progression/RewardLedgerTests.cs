using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using Game.Core.Progression;

namespace Game.Core.Tests.Progression;

public class RewardLedgerTests
{
    // ACC:T35.1
    [Fact]
    public void Should_Record_Reward_And_Replay_After_Save_Load()
    {
        var ledger = new RewardLedger();
        var grant = new RewardGrant(
            grantId: "grant-raid-001",
            guildId: "guild-1",
            sourceType: "raid",
            sourceId: "raid-01",
            rewards: new Dictionary<string, int>
            {
                ["gold"] = 100,
                ["gem"] = 2
            });

        ledger.Record(grant);

        var saved = ledger.Save();
        var loaded = RewardLedger.Load(saved);

        loaded.Replay().Should().BeEquivalentTo(new[] { grant }, options => options.WithStrictOrdering());
    }

    // ACC:T35.2
    [Fact]
    public void Should_Not_Record_Duplicate_Grant_Id()
    {
        var ledger = new RewardLedger();
        var grant = new RewardGrant(
            grantId: "grant-media-001",
            guildId: "guild-1",
            sourceType: "media",
            sourceId: "media-01",
            rewards: new Dictionary<string, int>
            {
                ["gold"] = 50
            });

        ledger.Record(grant);
        ledger.Record(grant);

        ledger.Replay().Should().ContainSingle().Which.Should().BeEquivalentTo(grant);
    }

    // ACC:T35.3
    [Fact]
    public void Should_Replay_In_Record_Order_For_Determinism()
    {
        var ledger = new RewardLedger();
        var grant1 = new RewardGrant(
            grantId: "grant-raid-002",
            guildId: "guild-1",
            sourceType: "raid",
            sourceId: "raid-02",
            rewards: new Dictionary<string, int>
            {
                ["gold"] = 30
            });
        var grant2 = new RewardGrant(
            grantId: "grant-media-002",
            guildId: "guild-1",
            sourceType: "media",
            sourceId: "media-02",
            rewards: new Dictionary<string, int>
            {
                ["gold"] = 20,
                ["gem"] = 1
            });

        ledger.Record(grant1);
        ledger.Record(grant2);

        ledger.Replay().Should().BeEquivalentTo(new[] { grant1, grant2 }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Should_Load_Empty_When_Data_Is_Whitespace()
    {
        var ledger = RewardLedger.Load(" ");

        ledger.Replay().Should().BeEmpty();
    }

    [Fact]
    public void Should_Throw_On_Invalid_Saved_Data()
    {
        var act = () => RewardLedger.Load("not-json");

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Reward ledger data is invalid.*");
    }

    [Fact]
    public void Should_Throw_On_Missing_GrantId_In_Saved_Data()
    {
        var json = "[{\"sourceType\":\"raid\",\"sourceId\":\"raid-01\",\"rewards\":{\"gold\":1}}]";

        var act = () => RewardLedger.Load(json);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Reward ledger data is invalid.*");
    }

    [Fact]
    public void Should_Throw_On_Null_Grant_In_Saved_Data()
    {
        var json = "[null]";

        var act = () => RewardLedger.Load(json);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Reward ledger data is invalid.*");
    }

    [Fact]
    public void Should_Throw_On_Null_Grant_Record()
    {
        var ledger = new RewardLedger();

        var act = () => ledger.Record(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Should_Throw_On_Empty_GrantId_Record()
    {
        var ledger = new RewardLedger();
        var grant = new RewardGrant(
            grantId: " ",
            guildId: "guild-1",
            sourceType: "raid",
            sourceId: "raid-01",
            rewards: new Dictionary<string, int>());

        var act = () => ledger.Record(grant);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_Throw_On_Empty_GuildId_Record()
    {
        var ledger = new RewardLedger();
        var grant = new RewardGrant(
            grantId: "grant-1",
            guildId: " ",
            sourceType: "raid",
            sourceId: "raid-01",
            rewards: new Dictionary<string, int>());

        var act = () => ledger.Record(grant);

        act.Should().Throw<ArgumentException>();
    }
}
