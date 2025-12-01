using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    #region Concurrency Tests

    [Fact]
    public async Task AddMember_ShouldHandleConcurrentAdds_WhenMultipleThreadsAddDifferentMembers()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "并发测试公会");
        var userIds = Enumerable.Range(1, 10).Select(i => $"user-{i}").ToList();

        // Act - 并发添加 10 个不同用户
        var tasks = userIds.Select(userId =>
            Task.Run(() => guild.AddMember(userId, GuildRole.Member))
        ).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue(), "所有不同用户都应成功添加");
        guild.Members.Should().HaveCount(11, "创建者 + 10个新成员");
        foreach (var userId in userIds)
        {
            guild.Members.Should().ContainSingle(m => m.UserId == userId);
        }
    }

    [Fact]
    public async Task AddMember_ShouldHandleConcurrentAdds_WhenMultipleThreadsAddSameUser()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "并发测试公会");
        var userId = "duplicate-user";

        // Act - 10 个线程同时尝试添加同一个用户
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() => guild.AddMember(userId, GuildRole.Member))
        ).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        var successCount = results.Count(r => r == true);
        successCount.Should().Be(1, "同一用户只能被添加一次");
        guild.Members.Should().HaveCount(2, "创建者 + 1个重复用户");
        guild.Members.Should().ContainSingle(m => m.UserId == userId);
    }

    [Fact]
    public async Task RemoveMember_ShouldHandleConcurrentRemoves_WhenMultipleThreadsRemoveDifferentMembers()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "并发测试公会");
        var userIds = Enumerable.Range(1, 10).Select(i => $"user-{i}").ToList();
        foreach (var userId in userIds)
        {
            guild.AddMember(userId, GuildRole.Member);
        }

        // Act - 并发移除 10 个不同用户
        var tasks = userIds.Select(userId =>
            Task.Run(() => guild.RemoveMember(userId))
        ).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue(), "所有成员都应成功移除");
        guild.Members.Should().HaveCount(1, "仅剩创建者");
        guild.Members.Should().ContainSingle(m => m.UserId == "creator-123");
    }

    [Fact]
    public async Task RemoveMember_ShouldHandleConcurrentRemoves_WhenMultipleThreadsRemoveSameUser()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "并发测试公会");
        var userId = "to-remove";
        guild.AddMember(userId, GuildRole.Member);

        // Act - 10 个线程同时尝试移除同一个用户
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() => guild.RemoveMember(userId))
        ).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        var successCount = results.Count(r => r == true);
        successCount.Should().Be(1, "同一用户只能被移除一次");
        guild.Members.Should().HaveCount(1, "仅剩创建者");
        guild.Members.Should().ContainSingle(m => m.UserId == "creator-123");
    }

    [Fact]
    public async Task ChangeRole_ShouldHandleConcurrentRoleChanges_WhenMultipleThreadsChangeDifferentMembers()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "并发测试公会");
        var userIds = Enumerable.Range(1, 10).Select(i => $"user-{i}").ToList();
        foreach (var userId in userIds)
        {
            guild.AddMember(userId, GuildRole.Member);
        }

        // Act - 并发修改 10 个不同用户的角色
        var tasks = userIds.Select(userId =>
            Task.Run(() => guild.ChangeRole(userId, GuildRole.Admin))
        ).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue(), "所有角色变更都应成功");
        guild.Members.Should().HaveCount(11);
        foreach (var userId in userIds)
        {
            guild.Members.Should().ContainSingle(m => m.UserId == userId && m.Role == GuildRole.Admin);
        }
    }

    [Fact]
    public async Task Guild_ShouldHandleMixedConcurrentOperations_WhenMultipleThreadsPerformDifferentActions()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "并发测试公会");

        // 预先添加一些成员
        for (int i = 1; i <= 5; i++)
        {
            guild.AddMember($"existing-{i}", GuildRole.Member);
        }

        // Act - 混合并发操作
        var tasks = new List<Task<object>>();

        // 5 个线程添加新成员
        for (int i = 1; i <= 5; i++)
        {
            var userId = $"new-{i}";
            tasks.Add(Task.Run<object>(() => guild.AddMember(userId, GuildRole.Member)));
        }

        // 3 个线程移除现有成员
        for (int i = 1; i <= 3; i++)
        {
            var userId = $"existing-{i}";
            tasks.Add(Task.Run<object>(() => guild.RemoveMember(userId)));
        }

        // 2 个线程修改角色
        for (int i = 4; i <= 5; i++)
        {
            var userId = $"existing-{i}";
            tasks.Add(Task.Run<object>(() => guild.ChangeRole(userId, GuildRole.Admin)));
        }

        await Task.WhenAll(tasks);

        // Assert - 验证最终状态一致性
        guild.Members.Should().NotBeNull();
        guild.Members.Should().Contain(m => m.UserId == "creator-123", "创建者应始终存在");

        // 验证新添加的成员存在
        for (int i = 1; i <= 5; i++)
        {
            guild.Members.Should().ContainSingle(m => m.UserId == $"new-{i}");
        }

        // 验证被移除的成员不存在
        for (int i = 1; i <= 3; i++)
        {
            guild.Members.Should().NotContain(m => m.UserId == $"existing-{i}");
        }

        // 验证角色变更成功
        for (int i = 4; i <= 5; i++)
        {
            guild.Members.Should().ContainSingle(m => m.UserId == $"existing-{i}" && m.Role == GuildRole.Admin);
        }
    }

    #endregion
}
