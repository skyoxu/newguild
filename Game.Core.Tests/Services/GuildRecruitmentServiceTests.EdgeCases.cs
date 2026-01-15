using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts.Guild;
using Game.Core.Contracts.Recruitment;
using Game.Core.Domain;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Services;

public sealed partial class GuildRecruitmentServiceTests
{
    [Fact]
    public async Task ApplyAsync_Should_NotPublish_When_CandidateId_IsEmpty()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var appliedAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApplyAsync(sut, guild, candidateId: " ", role: "member", appliedAt: appliedAt);

        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_Should_NotPublish_When_Role_IsInvalid()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var appliedAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApplyAsync(sut, guild, candidateId: "u-candidate", role: "invalid-role", appliedAt: appliedAt);

        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_Should_NotPublish_When_Role_IsWhitespace()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var appliedAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApplyAsync(sut, guild, candidateId: "u-candidate", role: " ", appliedAt: appliedAt);

        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_Should_NotPublish_When_Offer_AlreadyExists()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var appliedAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApplyAsync(sut, guild, candidateId: "u-candidate", role: "member", appliedAt: appliedAt);
        bus.Published.Should().ContainSingle(e => e.Type == RecruitmentOfferPresented.EventType);

        bus.Published.Clear();
        await RecruitmentServiceDriver.ApplyAsync(sut, guild, candidateId: "u-candidate", role: "member", appliedAt: appliedAt);

        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveAsync_Should_NotPublish_When_OfferId_IsMissing()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var resolvedAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApproveAsync(sut, guild, offerId: " ", approvedByUserId: "u-admin", resolvedAt: resolvedAt);

        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveAsync_Should_NotPublish_When_OfferId_IsNotFound()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var resolvedAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApproveAsync(sut, guild, offerId: "missing", approvedByUserId: "u-admin", resolvedAt: resolvedAt);

        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveAsync_Should_NotPublish_When_GuildDoesNotMatchOffer()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var otherGuild = new Guild(guildId: "g-2", creatorId: "u-admin2", name: "Other Guild");
        repo.Seed(otherGuild);

        var appliedAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z");
        var resolvedAt = DateTimeOffset.Parse("2030-01-01T00:01:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApplyAsync(sut, guild, candidateId: "u-candidate", role: "member", appliedAt: appliedAt);
        var offerId = bus.Published.Single(e => e.Type == RecruitmentOfferPresented.EventType)
            .Data.Should().BeOfType<RecruitmentOfferPresented>().Subject.OfferId;

        bus.Published.Clear();
        await RecruitmentServiceDriver.ApproveAsync(sut, otherGuild, offerId: offerId, approvedByUserId: "u-admin2", resolvedAt: resolvedAt);

        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task WithdrawAsync_Should_NotPublish_When_CandidateId_DoesNotMatch()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var appliedAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z");
        var resolvedAt = DateTimeOffset.Parse("2030-01-01T00:01:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApplyAsync(sut, guild, candidateId: "u-candidate", role: "member", appliedAt: appliedAt);
        var offerId = bus.Published.Single(e => e.Type == RecruitmentOfferPresented.EventType)
            .Data.Should().BeOfType<RecruitmentOfferPresented>().Subject.OfferId;

        bus.Published.Clear();
        await RecruitmentServiceDriver.WithdrawAsync(sut, guild, offerId: offerId, candidateId: "u-other", resolvedAt: resolvedAt);

        bus.Published.Should().NotContain(e => e.Type == GuildMemberJoined.EventType);
        bus.Published.Should().NotContain(e => e.Type == RecruitmentOfferResolved.EventType);
    }

    [Fact]
    public async Task WithdrawAsync_Should_NotPublish_When_OfferId_IsNotFound()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var resolvedAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.WithdrawAsync(sut, guild, offerId: "missing", candidateId: "u-candidate", resolvedAt: resolvedAt);

        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task WithdrawAsync_Should_NotPublish_When_OfferId_IsMissing()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var resolvedAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.WithdrawAsync(sut, guild, offerId: " ", candidateId: "u-candidate", resolvedAt: resolvedAt);

        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_Should_Publish_With_AdminRole_CaseInsensitive()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var appliedAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApplyAsync(sut, guild, candidateId: "u-candidate", role: "Admin", appliedAt: appliedAt);

        var presentedEvt = bus.Published
            .Should().ContainSingle(e => e.Type == RecruitmentOfferPresented.EventType)
            .Which;

        var presented = presentedEvt.Data.Should().BeOfType<RecruitmentOfferPresented>().Subject;
        presented.Role.Should().Be("admin");
    }

    [Fact]
    public async Task Methods_Should_ThrowArgumentNullException_When_GuildIsNull()
    {
        var (_, bus, repo, offers, roster) = Arrange();
        var timestamp = DateTimeOffset.Parse("2030-01-01T00:00:00Z");

        var sut = new GuildRecruitmentService(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await FluentActions.Invoking(() => sut.ApplyAsync(null!, "u", "member", timestamp))
            .Should().ThrowAsync<ArgumentNullException>();

        await FluentActions.Invoking(() => sut.ApproveAsync(null!, "o1", "u-admin", timestamp))
            .Should().ThrowAsync<ArgumentNullException>();

        await FluentActions.Invoking(() => sut.WithdrawAsync(null!, "o1", "u", timestamp))
            .Should().ThrowAsync<ArgumentNullException>();
    }
}
