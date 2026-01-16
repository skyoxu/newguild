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
        var name = "TestGuild";

        // Act
        var guild = new Guild(guildId, creatorId, name);

        // Assert
        guild.GuildId.Should().Be(guildId);
        guild.CreatorId.Should().Be(creatorId);
        guild.Name.Should().Be(name);
        guild.Members.Should().NotBeNull()
            .And.HaveCount(1, "creator should be the first member")
            .And.ContainSingle(m => m.UserId == creatorId && m.Role == GuildRole.Admin);
        guild.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ReconstructFromDatabase_ShouldCreateGuildWithCustomCreatedAtAndMembers()
    {
        // Arrange
        var guildId = "guild-001";
        var creatorId = "user-123";
        var name = "TestGuild";
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
        guild.CreatedAt.Should().Be(customCreatedAt, "should use database createdAt");
        guild.Members.Should().HaveCount(3);
        guild.Members.Should().ContainSingle(m => m.UserId == creatorId && m.Role == GuildRole.Admin);
        guild.Members.Should().ContainSingle(m => m.UserId == "user-456" && m.Role == GuildRole.Member);
        guild.Members.Should().ContainSingle(m => m.UserId == "user-789" && m.Role == GuildRole.Member);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowException_WhenGuildIdIsInvalid(string? invalidGuildId)
    {
        // Arrange & Act
        var act = () => new Guild(invalidGuildId!, "creator-123", "GuildName");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("guildId")
            .WithMessage("*GuildId cannot be empty.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowException_WhenCreatorIdIsInvalid(string? invalidCreatorId)
    {
        // Arrange & Act
        var act = () => new Guild("guild-001", invalidCreatorId!, "GuildName");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("creatorId")
            .WithMessage("*CreatorId cannot be empty.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowException_WhenNameIsInvalid(string? invalidName)
    {
        // Arrange & Act
        var act = () => new Guild("guild-001", "creator-123", invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Name cannot be empty.*");
    }

    #endregion

    #region AddMember Tests

    [Fact]
    public void AddMember_ShouldAddNewMember_WhenUserNotInGuild()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "TestGuild");
        var newUserId = "user-456";

        // Act
        var result = guild.AddMember(newUserId, GuildRole.Member);

        // Assert
        result.Should().BeTrue("new member should be added");
        guild.Members.Should().HaveCount(2)
            .And.Contain(m => m.UserId == newUserId && m.Role == GuildRole.Member);
    }

    [Fact]
    public void AddMember_ShouldReturnFalse_WhenUserAlreadyInGuild()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "TestGuild");
        var userId = "user-456";
        guild.AddMember(userId, GuildRole.Member);

        // Act
        var result = guild.AddMember(userId, GuildRole.Member);

        // Assert
        result.Should().BeFalse("duplicate add should return false");
        guild.Members.Should().HaveCount(2, "should not add duplicates");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddMember_ShouldThrowException_WhenUserIdIsInvalid(string? invalidUserId)
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "TestGuild");

        // Act
        var act = () => guild.AddMember(invalidUserId!, GuildRole.Member);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*UserId cannot be empty.*");
    }

    #endregion

    #region RemoveMember Tests

    [Fact]
    public void RemoveMember_ShouldRemoveExistingMember_WhenNotCreator()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "TestGuild");
        var memberId = "user-456";
        guild.AddMember(memberId, GuildRole.Member);

        // Act
        var result = guild.RemoveMember(memberId);

        // Assert
        result.Should().BeTrue("regular member should be removed");
        guild.Members.Should().HaveCount(1)
            .And.NotContain(m => m.UserId == memberId);
    }

    [Fact]
    public void RemoveMember_ShouldReturnFalse_WhenRemovingCreator()
    {
        // Arrange
        var creatorId = "creator-123";
        var guild = new Guild("guild-001", creatorId, "TestGuild");

        // Act
        var result = guild.RemoveMember(creatorId);

        // Assert
        result.Should().BeFalse("creator cannot be removed");
        guild.Members.Should().HaveCount(1)
            .And.ContainSingle(m => m.UserId == creatorId);
    }

    [Fact]
    public void RemoveMember_ShouldReturnFalse_WhenUserNotInGuild()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "TestGuild");

        // Act
        var result = guild.RemoveMember("nonexistent-user");

        // Assert
        result.Should().BeFalse("non-existent user should return false");
        guild.Members.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RemoveMember_ShouldThrowException_WhenUserIdIsInvalid(string? invalidUserId)
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "TestGuild");

        // Act
        var act = () => guild.RemoveMember(invalidUserId!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*UserId cannot be empty.*");
    }

    #endregion

    #region ChangeRole Tests

    [Fact]
    public void ChangeRole_ShouldUpdateMemberRole_WhenUserExists()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "TestGuild");
        var memberId = "user-456";
        guild.AddMember(memberId, GuildRole.Member);

        // Act
        var result = guild.ChangeRole(memberId, GuildRole.Admin);

        // Assert
        result.Should().BeTrue("role change should succeed");
        guild.Members.Should().Contain(m => m.UserId == memberId && m.Role == GuildRole.Admin);
    }

    [Fact]
    public void ChangeRole_ShouldReturnFalse_WhenUserNotInGuild()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "TestGuild");

        // Act
        var result = guild.ChangeRole("nonexistent-user", GuildRole.Admin);

        // Assert
        result.Should().BeFalse("non-existent user should return false");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangeRole_ShouldThrowException_WhenUserIdIsInvalid(string? invalidUserId)
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "TestGuild");

        // Act
        var act = () => guild.ChangeRole(invalidUserId!, GuildRole.Admin);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*UserId cannot be empty.*");
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task AddMember_ShouldHandleConcurrentAdds_WhenMultipleThreadsAddDifferentMembers()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "ConcurrentTestGuild");
        var userIds = Enumerable.Range(1, 10).Select(i => $"user-{i}").ToList();

        // Act - concurrently add 10 distinct users
        var tasks = userIds.Select(userId =>
            Task.Run(() => guild.AddMember(userId, GuildRole.Member))
        ).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue(), "all distinct users should be added");
        guild.Members.Should().HaveCount(11, "creator + 10 new members");
        foreach (var userId in userIds)
        {
            guild.Members.Should().ContainSingle(m => m.UserId == userId);
        }
    }

    [Fact]
    public async Task AddMember_ShouldHandleConcurrentAdds_WhenMultipleThreadsAddSameUser()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "ConcurrentTestGuild");
        var userId = "duplicate-user";

        // Act - 10 threads attempt to add the same user
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() => guild.AddMember(userId, GuildRole.Member))
        ).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        var successCount = results.Count(r => r == true);
        successCount.Should().Be(1, "the same user can only be added once");
        guild.Members.Should().HaveCount(2, "creator + 1 user");
        guild.Members.Should().ContainSingle(m => m.UserId == userId);
    }

    [Fact]
    public async Task RemoveMember_ShouldHandleConcurrentRemoves_WhenMultipleThreadsRemoveDifferentMembers()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "ConcurrentTestGuild");
        var userIds = Enumerable.Range(1, 10).Select(i => $"user-{i}").ToList();
        foreach (var userId in userIds)
        {
            guild.AddMember(userId, GuildRole.Member);
        }

        // Act - concurrently remove 10 distinct users
        var tasks = userIds.Select(userId =>
            Task.Run(() => guild.RemoveMember(userId))
        ).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue(), "all members should be removed");
        guild.Members.Should().HaveCount(1, "only the creator remains");
        guild.Members.Should().ContainSingle(m => m.UserId == "creator-123");
    }

    [Fact]
    public async Task RemoveMember_ShouldHandleConcurrentRemoves_WhenMultipleThreadsRemoveSameUser()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "ConcurrentTestGuild");
        var userId = "to-remove";
        guild.AddMember(userId, GuildRole.Member);

        // Act - 10 threads attempt to remove the same user
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() => guild.RemoveMember(userId))
        ).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        var successCount = results.Count(r => r == true);
        successCount.Should().Be(1, "the same user can only be removed once");
        guild.Members.Should().HaveCount(1, "only the creator remains");
        guild.Members.Should().ContainSingle(m => m.UserId == "creator-123");
    }

    [Fact]
    public async Task ChangeRole_ShouldHandleConcurrentRoleChanges_WhenMultipleThreadsChangeDifferentMembers()
    {
        // Arrange
        var guild = new Guild("guild-001", "creator-123", "ConcurrentTestGuild");
        var userIds = Enumerable.Range(1, 10).Select(i => $"user-{i}").ToList();
        foreach (var userId in userIds)
        {
            guild.AddMember(userId, GuildRole.Member);
        }

        // Act - concurrently change roles for 10 distinct users
        var tasks = userIds.Select(userId =>
            Task.Run(() => guild.ChangeRole(userId, GuildRole.Admin))
        ).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue(), "all role changes should succeed");
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
        var guild = new Guild("guild-001", "creator-123", "ConcurrentTestGuild");

        // Pre-add some members
        for (int i = 1; i <= 5; i++)
        {
            guild.AddMember($"existing-{i}", GuildRole.Member);
        }

        // Act - mixed concurrent operations
        var tasks = new List<Task<object>>();

        // 5 threads add new members
        for (int i = 1; i <= 5; i++)
        {
            var userId = $"new-{i}";
            tasks.Add(Task.Run<object>(() => guild.AddMember(userId, GuildRole.Member)));
        }

        // 3 threads remove existing members
        for (int i = 1; i <= 3; i++)
        {
            var userId = $"existing-{i}";
            tasks.Add(Task.Run<object>(() => guild.RemoveMember(userId)));
        }

        // 2 threads change roles
        for (int i = 4; i <= 5; i++)
        {
            var userId = $"existing-{i}";
            tasks.Add(Task.Run<object>(() => guild.ChangeRole(userId, GuildRole.Admin)));
        }

        await Task.WhenAll(tasks);

        // Assert - validate final state consistency
        guild.Members.Should().NotBeNull();
        guild.Members.Should().Contain(m => m.UserId == "creator-123", "creator should always exist");

        // Verify newly-added members exist
        for (int i = 1; i <= 5; i++)
        {
            guild.Members.Should().ContainSingle(m => m.UserId == $"new-{i}");
        }

        // Verify removed members do not exist
        for (int i = 1; i <= 3; i++)
        {
            guild.Members.Should().NotContain(m => m.UserId == $"existing-{i}");
        }

        // Verify role changes
        for (int i = 4; i <= 5; i++)
        {
            guild.Members.Should().ContainSingle(m => m.UserId == $"existing-{i}" && m.Role == GuildRole.Admin);
        }
    }

    #endregion

    #region ReconstructFromDatabase Factory Method Tests

    [Fact]
    public void ReconstructFromDatabase_ShouldCreateGuildWithValidParameters()
    {
        // Arrange
        var guildId = "guild-001";
        var creatorId = "creator-123";
        var name = "TestGuild";
        var createdAt = DateTimeOffset.UtcNow;
        var members = new List<GuildMember>
        {
            new GuildMember(creatorId, GuildRole.Admin)
        };

        // Act
        var guild = Guild.ReconstructFromDatabase(guildId, creatorId, name, createdAt, members);

        // Assert
        guild.Should().NotBeNull("ReconstructFromDatabase should create guild with valid parameters");
        guild.GuildId.Should().Be(guildId);
        guild.CreatorId.Should().Be(creatorId);
        guild.Name.Should().Be(name);
        guild.CreatedAt.Should().Be(createdAt);
        guild.Members.Should().HaveCount(1);
        guild.Members.Should().ContainSingle(m => m.UserId == creatorId && m.Role == GuildRole.Admin);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReconstructFromDatabase_ShouldThrowException_WhenGuildIdIsInvalid(string? invalidGuildId)
    {
        // Arrange
        var members = new List<GuildMember>
        {
            new GuildMember("creator-123", GuildRole.Admin)
        };

        // Act
        var act = () => Guild.ReconstructFromDatabase(invalidGuildId!, "creator-123", "GuildName", DateTimeOffset.UtcNow, members);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("guildId")
            .WithMessage("*GuildId cannot be empty.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReconstructFromDatabase_ShouldThrowException_WhenCreatorIdIsInvalid(string? invalidCreatorId)
    {
        // Arrange
        var members = new List<GuildMember>
        {
            new GuildMember("creator-123", GuildRole.Admin)
        };

        // Act
        var act = () => Guild.ReconstructFromDatabase("guild-001", invalidCreatorId!, "GuildName", DateTimeOffset.UtcNow, members);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("creatorId")
            .WithMessage("*CreatorId cannot be empty.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReconstructFromDatabase_ShouldThrowException_WhenNameIsInvalid(string? invalidName)
    {
        // Arrange
        var members = new List<GuildMember>
        {
            new GuildMember("creator-123", GuildRole.Admin)
        };

        // Act
        var act = () => Guild.ReconstructFromDatabase("guild-001", "creator-123", invalidName!, DateTimeOffset.UtcNow, members);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Name cannot be empty.*");
    }

    [Fact]
    public void ReconstructFromDatabase_ShouldThrowException_WhenMembersIsNull()
    {
        // Act
        var act = () => Guild.ReconstructFromDatabase("guild-001", "creator-123", "GuildName", DateTimeOffset.UtcNow, members: null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("members")
            .WithMessage("*Members cannot be empty.*");
    }

    [Fact]
    public void ReconstructFromDatabase_ShouldThrowException_WhenMembersIsEmpty()
    {
        // Arrange
        var emptyMembers = new List<GuildMember>();

        // Act
        var act = () => Guild.ReconstructFromDatabase("guild-001", "creator-123", "GuildName", DateTimeOffset.UtcNow, emptyMembers);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("members")
            .WithMessage("*Members cannot be empty.*");
    }

    #endregion
}
