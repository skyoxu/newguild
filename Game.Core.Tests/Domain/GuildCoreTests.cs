using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Game.Core.Domain;
using Xunit;

namespace Game.Core.Tests.Domain;

/// <summary>
/// TDD tests for Guild entity following ADR-0005 quality gates.
/// Coverage target: ≥90% lines, ≥85% branches.
/// </summary>
public class GuildCoreTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldCreateGuildWithValidParameters()
    {
        // Arrange
        var guildId = "guild-001";
        var creatorId = "user-123";
        var name = "测试公会";

        // Act
        var guild = new Guild(guildId, creatorId, name);

        // Assert
        guild.GuildId.Should().Be(guildId);
        guild.CreatorId.Should().Be(creatorId);
        guild.Name.Should().Be(name);
        guild.Members.Should().NotBeNull()
            .And.HaveCount(1, "创建者应自动成为第一个成员")
            .And.ContainSingle(m => m.UserId == creatorId && m.Role == GuildRole.Admin);
        guild.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ReconstructFromDatabase_ShouldCreateGuildWithCustomCreatedAtAndMembers()
    {
        // Arrange
        var guildId = "guild-001";
        var creatorId = "user-123";
        var name = "测试公会";
        var customCreatedAt = DateTimeOffset.UtcNow.AddDays(-30);
        var members = new List<GuildMember>
        {
            new GuildMember(creatorId, GuildRole.Admin),
            new GuildMember("user-456", GuildRole.Member),
            new GuildMember("user-789", GuildRole.Member)
        };

        // Act
        var guild = Guild.ReconstructFromDatabase(guildId, creatorId, name, customCreatedAt, members);

        // Assert
        guild.GuildId.Should().Be(guildId);
        guild.CreatorId.Should().Be(creatorId);
        guild.Name.Should().Be(name);
        guild.CreatedAt.Should().Be(customCreatedAt, "应使用数据库中的创建时间");
        guild.Members.Should().HaveCount(3);
        guild.Members.Should().ContainSingle(m => m.UserId == creatorId && m.Role == GuildRole.Admin);
        guild.Members.Should().ContainSingle(m => m.UserId == "user-456" && m.Role == GuildRole.Member);
        guild.Members.Should().ContainSingle(m => m.UserId == "user-789" && m.Role == GuildRole.Member);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowException_WhenGuildIdIsInvalid(string invalidGuildId)
    {
        // Arrange & Act
        var act = () => new Guild(invalidGuildId, "creator-123", "公会名称");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("guildId")
            .WithMessage("*公会ID不能为空*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowException_WhenCreatorIdIsInvalid(string invalidCreatorId)
    {
        // Arrange & Act
        var act = () => new Guild("guild-001", invalidCreatorId, "公会名称");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("creatorId")
            .WithMessage("*创建者ID不能为空*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowException_WhenNameIsInvalid(string invalidName)
    {
        // Arrange & Act
        var act = () => new Guild("guild-001", "creator-123", invalidName);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*公会名称不能为空*");
    }

    #endregion

    #region AddMember Tests

    [Fact]
    public void AddMember_ShouldAddNewMember_WhenUserNotInGuild()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "测试公会");
        var newUserId = "user-456";

        // Act
        var result = guild.AddMember(newUserId, GuildRole.Member);

        // Assert
        result.Should().BeTrue("新成员应成功加入");
        guild.Members.Should().HaveCount(2)
            .And.Contain(m => m.UserId == newUserId && m.Role == GuildRole.Member);
    }

    [Fact]
    public void AddMember_ShouldReturnFalse_WhenUserAlreadyInGuild()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "测试公会");
        var userId = "user-456";
        guild.AddMember(userId, GuildRole.Member);

        // Act
        var result = guild.AddMember(userId, GuildRole.Member);

        // Assert
        result.Should().BeFalse("重复添加应返回false");
        guild.Members.Should().HaveCount(2, "不应重复添加成员");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddMember_ShouldThrowException_WhenUserIdIsInvalid(string invalidUserId)
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "测试公会");

        // Act
        var act = () => guild.AddMember(invalidUserId, GuildRole.Member);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*用户ID不能为空*");
    }

    #endregion

    #region RemoveMember Tests

    [Fact]
    public void RemoveMember_ShouldRemoveExistingMember_WhenNotCreator()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "测试公会");
        var memberId = "user-456";
        guild.AddMember(memberId, GuildRole.Member);

        // Act
        var result = guild.RemoveMember(memberId);

        // Assert
        result.Should().BeTrue("普通成员应可以被移除");
        guild.Members.Should().HaveCount(1)
            .And.NotContain(m => m.UserId == memberId);
    }

    [Fact]
    public void RemoveMember_ShouldReturnFalse_WhenRemovingCreator()
    {
        // Arrange
        var creatorId = "creator-123";
        var guild = new Guild("guild-001", creatorId, "测试公会");

        // Act
        var result = guild.RemoveMember(creatorId);

        // Assert
        result.Should().BeFalse("创建者不能被移除");
        guild.Members.Should().HaveCount(1)
            .And.ContainSingle(m => m.UserId == creatorId);
    }

    [Fact]
    public void RemoveMember_ShouldReturnFalse_WhenUserNotInGuild()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "测试公会");

        // Act
        var result = guild.RemoveMember("nonexistent-user");

        // Assert
        result.Should().BeFalse("不存在的用户应返回false");
        guild.Members.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RemoveMember_ShouldThrowException_WhenUserIdIsInvalid(string invalidUserId)
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "测试公会");

        // Act
        var act = () => guild.RemoveMember(invalidUserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*用户ID不能为空*");
    }

    #endregion

    #region ChangeRole Tests

    [Fact]
    public void ChangeRole_ShouldUpdateMemberRole_WhenUserExists()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "测试公会");
        var memberId = "user-456";
        guild.AddMember(memberId, GuildRole.Member);

        // Act
        var result = guild.ChangeRole(memberId, GuildRole.Admin);

        // Assert
        result.Should().BeTrue("角色变更应成功");
        guild.Members.Should().Contain(m => m.UserId == memberId && m.Role == GuildRole.Admin);
    }

    [Fact]
    public void ChangeRole_ShouldReturnFalse_WhenUserNotInGuild()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "测试公会");

        // Act
        var result = guild.ChangeRole("nonexistent-user", GuildRole.Admin);

        // Assert
        result.Should().BeFalse("不存在的用户应返回false");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangeRole_ShouldThrowException_WhenUserIdIsInvalid(string invalidUserId)
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "测试公会");

        // Act
        var act = () => guild.ChangeRole(invalidUserId, GuildRole.Admin);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*用户ID不能为空*");
    }

    #endregion
}
