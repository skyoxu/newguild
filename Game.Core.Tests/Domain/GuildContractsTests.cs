using System;
using FluentAssertions;
using Game.Contracts.Guild;
using Xunit;

namespace Game.Core.Tests.Domain;

public class GuildContractsTests
{
    [Fact]
    public void GuildCreated_EventType_should_match_expected()
    {
        GuildCreated.EventType.Should().Be("core.guild.created");
    }

    [Fact]
    public void GuildCreated_should_accept_valid_fields()
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
    public void GuildMemberJoined_EventType_should_match_expected()
    {
        GuildMemberJoined.EventType.Should().Be("core.guild.member.joined");
    }

    [Fact]
    public void GuildMemberJoined_should_accept_valid_fields()
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
    public void GuildMemberLeft_EventType_should_match_expected()
    {
        GuildMemberLeft.EventType.Should().Be("core.guild.member.left");
    }

    [Fact]
    public void GuildMemberLeft_should_accept_valid_fields()
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
    public void GuildDisbanded_EventType_should_match_expected()
    {
        GuildDisbanded.EventType.Should().Be("core.guild.disbanded");
    }

    [Fact]
    public void GuildDisbanded_should_accept_valid_fields()
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
    public void GuildMemberRoleChanged_EventType_should_match_expected()
    {
        GuildMemberRoleChanged.EventType.Should().Be("core.guild.member.role_changed");
    }

    [Fact]
    public void GuildMemberRoleChanged_should_accept_valid_fields()
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
}
