using System;
using FluentAssertions;
using Game.Core.Contracts.AI;
using Game.Core.Contracts.Media;
using Game.Core.Contracts.Raid;
using Game.Core.Contracts.Recruitment;
using Game.Core.Contracts.Social;
using Xunit;

namespace Game.Core.Tests.Contracts;

public class T3EventContractsTests
{
    [Fact]
    public void RecruitmentOfferPresented_EventType_Should_Match_Expected()
    {
        RecruitmentOfferPresented.EventType.Should().Be("core.recruitment.offer.presented");
    }

    [Fact]
    public void RecruitmentOfferResolved_EventType_Should_Match_Expected()
    {
        RecruitmentOfferResolved.EventType.Should().Be("core.recruitment.offer.resolved");
    }

    [Fact]
    public void AiCycleStarted_EventType_Should_Match_Expected()
    {
        AiCycleStarted.EventType.Should().Be("core.ai.cycle.started");
    }

    [Fact]
    public void AiIntentIssued_EventType_Should_Match_Expected()
    {
        AiIntentIssued.EventType.Should().Be("core.ai.intent.issued");
    }

    [Fact]
    public void AiCycleCompleted_EventType_Should_Match_Expected()
    {
        AiCycleCompleted.EventType.Should().Be("core.ai.cycle.completed");
    }

    [Fact]
    public void AiEcosystemStepCompleted_EventType_Should_Match_Expected()
    {
        AiEcosystemStepCompleted.EventType.Should().Be("core.ai.ecosystem.step.completed");
    }

    [Fact]
    public void RaidScheduled_EventType_Should_Match_Expected()
    {
        RaidScheduled.EventType.Should().Be("core.raid.scheduled");
    }

    [Fact]
    public void RaidResolved_EventType_Should_Match_Expected()
    {
        RaidResolved.EventType.Should().Be("core.raid.resolved");
    }

    [Fact]
    public void SocialInteractionTriggered_EventType_Should_Match_Expected()
    {
        SocialInteractionTriggered.EventType.Should().Be("core.social.interaction.triggered");
    }

    [Fact]
    public void SocialRelationshipChanged_EventType_Should_Match_Expected()
    {
        SocialRelationshipChanged.EventType.Should().Be("core.social.relationship.changed");
    }

    [Fact]
    public void MediaBeatTriggered_EventType_Should_Match_Expected()
    {
        MediaBeatTriggered.EventType.Should().Be("core.media.beat.triggered");
    }

    [Fact]
    public void ReputationChanged_EventType_Should_Match_Expected()
    {
        ReputationChanged.EventType.Should().Be("core.reputation.changed");
    }

    [Fact]
    public void Contracts_Should_Accept_Valid_Fields()
    {
        var now = DateTimeOffset.UtcNow;

        _ = new RecruitmentOfferPresented(
            OfferId: "offer-1",
            GuildId: "guild-1",
            CandidateId: "npc-1",
            Role: "member",
            PresentedAt: now
        );

        _ = new RecruitmentOfferResolved(
            OfferId: "offer-1",
            GuildId: "guild-1",
            CandidateId: "npc-1",
            Decision: "accepted",
            Reason: "player_choice",
            ResolvedAt: now
        );

        _ = new AiCycleStarted(SaveId: "save-1", Week: 1, StartedAt: now);
        _ = new AiIntentIssued(
            SaveId: "save-1",
            Week: 1,
            IntentId: "intent-1",
            IntentType: "recruitment",
            ActorId: "npc-guild-1",
            TargetId: "guild-1",
            IssuedAt: now
        );
        _ = new AiCycleCompleted(SaveId: "save-1", Week: 1, IntentsIssued: 1, CompletedAt: now);
        _ = new AiEcosystemStepCompleted(SaveId: "save-1", Week: 1, Summary: "ok", CompletedAt: now);

        _ = new RaidScheduled(RaidId: "raid-1", GuildId: "guild-1", Week: 1, EncounterId: "enc-1", ScheduledAt: now);
        _ = new RaidResolved(RaidId: "raid-1", GuildId: "guild-1", Week: 1, Result: "success", RewardPoints: 10, ResolvedAt: now);

        _ = new SocialInteractionTriggered(
            InteractionId: "social-1",
            GuildId: "guild-1",
            ActorId: "npc-1",
            TargetId: "npc-2",
            InteractionType: "chat",
            TriggeredAt: now
        );

        _ = new SocialRelationshipChanged(
            GuildId: "guild-1",
            SubjectId: "npc-1",
            OtherId: "npc-2",
            OldValue: 0,
            NewValue: 1,
            ChangedAt: now
        );

        _ = new MediaBeatTriggered(
            BeatId: "beat-1",
            GuildId: "guild-1",
            SourceEventType: RaidResolved.EventType,
            Headline: "Headline",
            TriggeredAt: now
        );

        _ = new ReputationChanged(
            GuildId: "guild-1",
            OldValue: 10,
            NewValue: 11,
            Reason: "raid_success",
            ChangedAt: now
        );
    }
}

