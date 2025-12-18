using System;
using FluentAssertions;
using Game.Core.Contracts.Guild;
using Xunit;

namespace Game.Core.Tests.Domain;

public class GuildContractsTests
{
    [Fact]
    public void GuildCreated_EventType_Should_Match_Expected()
    {
        GuildCreated.EventType.Should().Be("core.guild.created");
    }

    [Fact]
    public void GuildCreated_Should_Accept_Valid_Fields()
    {
        var now = DateTimeOffset.UtcNow;

        var evt = new GuildCreated(
            GuildId: "g1",
            CreatorId: "u1",
            GuildName: "TestGuild",
            CreatedAt: now
        );

        evt.GuildId.Should().Be("g1");
        evt.CreatorId.Should().Be("u1");
        evt.GuildName.Should().Be("TestGuild");
        evt.CreatedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GuildMemberJoined_EventType_Should_Match_Expected()
    {
        GuildMemberJoined.EventType.Should().Be("core.guild.member.joined");
    }

    [Fact]
    public void GuildMemberJoined_Should_Accept_Valid_Fields()
    {
        var now = DateTimeOffset.UtcNow;

        var evt = new GuildMemberJoined(
            UserId: "u1",
            GuildId: "g1",
            JoinedAt: now,
            Role: "member"
        );

        evt.UserId.Should().Be("u1");
        evt.GuildId.Should().Be("g1");
        evt.JoinedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        evt.Role.Should().Be("member");
    }

    [Fact]
    public void GuildMemberLeft_EventType_Should_Match_Expected()
    {
        GuildMemberLeft.EventType.Should().Be("core.guild.member.left");
    }

    [Fact]
    public void GuildMemberLeft_Should_Accept_Valid_Fields()
    {
        var now = DateTimeOffset.UtcNow;

        var evt = new GuildMemberLeft(
            UserId: "u1",
            GuildId: "g1",
            LeftAt: now,
            Reason: "voluntary"
        );

        evt.UserId.Should().Be("u1");
        evt.GuildId.Should().Be("g1");
        evt.LeftAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        evt.Reason.Should().Be("voluntary");
    }

    [Fact]
    public void GuildDisbanded_EventType_Should_Match_Expected()
    {
        GuildDisbanded.EventType.Should().Be("core.guild.disbanded");
    }

    [Fact]
    public void GuildDisbanded_Should_Accept_Valid_Fields()
    {
        var now = DateTimeOffset.UtcNow;

        var evt = new GuildDisbanded(
            GuildId: "g1",
            DisbandedByUserId: "u-admin",
            DisbandedAt: now,
            Reason: "cleanup"
        );

        evt.GuildId.Should().Be("g1");
        evt.DisbandedByUserId.Should().Be("u-admin");
        evt.DisbandedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        evt.Reason.Should().Be("cleanup");
    }

    [Fact]
    public void GuildMemberRoleChanged_EventType_Should_Match_Expected()
    {
        GuildMemberRoleChanged.EventType.Should().Be("core.guild.member.role_changed");
    }

    [Fact]
    public void GuildMemberRoleChanged_Should_Accept_Valid_Fields()
    {
        var now = DateTimeOffset.UtcNow;

        var evt = new GuildMemberRoleChanged(
            UserId: "u1",
            GuildId: "g1",
            OldRole: "member",
            NewRole: "admin",
            ChangedAt: now,
            ChangedByUserId: "u-admin"
        );

        evt.UserId.Should().Be("u1");
        evt.GuildId.Should().Be("g1");
        evt.OldRole.Should().Be("member");
        evt.NewRole.Should().Be("admin");
        evt.ChangedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        evt.ChangedByUserId.Should().Be("u-admin");
    }

    [Fact]
    public void GuildCreated_Equality_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var evt1 = new GuildCreated("g1", "u1", "Guild", now);
        var evt2 = new GuildCreated("g1", "u1", "Guild", now);

        // Act & Assert
        evt1.Should().Be(evt2);
        (evt1 == evt2).Should().BeTrue();
    }

    [Fact]
    public void GuildCreated_Equality_WithDifferentValues_ReturnsFalse()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var evt1 = new GuildCreated("g1", "u1", "Guild1", now);
        var evt2 = new GuildCreated("g1", "u1", "Guild2", now);

        // Act & Assert
        evt1.Should().NotBe(evt2);
        (evt1 == evt2).Should().BeFalse();
    }

    [Fact]
    public void GuildMemberJoined_Equality_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var evt1 = new GuildMemberJoined("u1", "g1", now, "member");
        var evt2 = new GuildMemberJoined("u1", "g1", now, "member");

        // Act & Assert
        evt1.Should().Be(evt2);
    }

    [Fact]
    public void GuildMemberLeft_WithEmptyReason_AcceptsEmptyString()
    {
        // Arrange & Act
        var evt = new GuildMemberLeft(
            UserId: "u1",
            GuildId: "g1",
            LeftAt: DateTimeOffset.UtcNow,
            Reason: ""
        );

        // Assert
        evt.Reason.Should().BeEmpty();
    }

    [Fact]
    public void GuildDisbanded_WithEmptyReason_AcceptsEmptyString()
    {
        // Arrange & Act
        var evt = new GuildDisbanded(
            GuildId: "g1",
            DisbandedByUserId: "u1",
            DisbandedAt: DateTimeOffset.UtcNow,
            Reason: ""
        );

        // Assert
        evt.Reason.Should().BeEmpty();
    }
}
