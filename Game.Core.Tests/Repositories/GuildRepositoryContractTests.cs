using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Domain;
using Game.Core.Repositories;
using Xunit;

namespace Game.Core.Tests.Repositories;

/// <summary>
/// Abstract contract tests for IGuildRepository implementations.
/// Any concrete repository implementation must pass these tests.
/// Follows ADR-0005 quality gates (≥90% coverage, ≥85% branches).
/// </summary>
public abstract class GuildRepositoryContractTests
{
    /// <summary>
    /// Factory method to create a fresh repository instance for each test.
    /// Implementations must provide a clean repository state.
    /// </summary>
    protected abstract IGuildRepository CreateRepository();

    #region Create Tests

    [Fact]
    public async Task CreateAsync_ShouldPersistGuild_WhenValidGuildProvided()
    {
        // Arrange
        var repo = CreateRepository();
        var guild = new Guild("guild-001", "creator-123", "Test Guild");

        // Act
        var created = await repo.CreateAsync(guild);

        // Assert
        created.Should().NotBeNull();
        created.GuildId.Should().Be("guild-001");
        created.Name.Should().Be("Test Guild");
        created.CreatorId.Should().Be("creator-123");
        created.Members.Should().HaveCount(1)
            .And.ContainSingle(m => m.UserId == "creator-123" && m.Role == GuildRole.Admin);
    }

    [Fact]
    public async Task CreateAsync_ShouldPreserveCreatedAt_WhenPersisting()
    {
        // Arrange
        var repo = CreateRepository();
        var guild = new Guild("guild-002", "creator-456", "Time Test Guild");
        var originalCreatedAt = guild.CreatedAt;

        // Act
        var created = await repo.CreateAsync(guild);

        // Assert
        created.CreatedAt.Should().BeCloseTo(originalCreatedAt, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnGuild_WhenGuildExists()
    {
        // Arrange
        var repo = CreateRepository();
        var guild = new Guild("guild-003", "creator-789", "Existing Guild");
        await repo.CreateAsync(guild);

        // Act
        var retrieved = await repo.GetByIdAsync("guild-003");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.GuildId.Should().Be("guild-003");
        retrieved.Name.Should().Be("Existing Guild");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenGuildDoesNotExist()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var retrieved = await repo.GetByIdAsync("nonexistent-guild");

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges_WhenMemberAdded()
    {
        // Arrange
        var repo = CreateRepository();
        var guild = new Guild("guild-004", "creator-111", "Update Test Guild");
        await repo.CreateAsync(guild);

        // Act
        guild.AddMember("user-222", GuildRole.Member);
        var updated = await repo.UpdateAsync(guild);

        // Assert
        updated.Members.Should().HaveCount(2)
            .And.Contain(m => m.UserId == "user-222" && m.Role == GuildRole.Member);

        // Verify persistence
        var retrieved = await repo.GetByIdAsync("guild-004");
        retrieved!.Members.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges_WhenMemberRemoved()
    {
        // Arrange
        var repo = CreateRepository();
        var guild = new Guild("guild-005", "creator-333", "Removal Test Guild");
        guild.AddMember("user-444", GuildRole.Member);
        await repo.CreateAsync(guild);

        // Act
        guild.RemoveMember("user-444");
        var updated = await repo.UpdateAsync(guild);

        // Assert
        updated.Members.Should().HaveCount(1)
            .And.NotContain(m => m.UserId == "user-444");

        // Verify persistence
        var retrieved = await repo.GetByIdAsync("guild-005");
        retrieved!.Members.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges_WhenRoleChanged()
    {
        // Arrange
        var repo = CreateRepository();
        var guild = new Guild("guild-006", "creator-555", "Role Change Test Guild");
        guild.AddMember("user-666", GuildRole.Member);
        await repo.CreateAsync(guild);

        // Act
        guild.ChangeRole("user-666", GuildRole.Admin);
        var updated = await repo.UpdateAsync(guild);

        // Assert
        updated.Members.Should().Contain(m => m.UserId == "user-666" && m.Role == GuildRole.Admin);

        // Verify persistence
        var retrieved = await repo.GetByIdAsync("guild-006");
        retrieved!.Members.Should().Contain(m => m.UserId == "user-666" && m.Role == GuildRole.Admin);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_ShouldRemoveGuild_WhenGuildExists()
    {
        // Arrange
        var repo = CreateRepository();
        var guild = new Guild("guild-007", "creator-777", "Delete Test Guild");
        await repo.CreateAsync(guild);

        // Act
        var result = await repo.DeleteAsync("guild-007");

        // Assert
        result.Should().BeTrue();

        // Verify deletion
        var retrieved = await repo.GetByIdAsync("guild-007");
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenGuildDoesNotExist()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var result = await repo.DeleteAsync("nonexistent-guild");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllGuilds_WhenMultipleExist()
    {
        // Arrange
        var repo = CreateRepository();
        await repo.CreateAsync(new Guild("guild-008", "creator-888", "Guild A"));
        await repo.CreateAsync(new Guild("guild-009", "creator-999", "Guild B"));
        await repo.CreateAsync(new Guild("guild-010", "creator-000", "Guild C"));

        // Act
        var all = await repo.GetAllAsync();

        // Assert
        all.Should().HaveCount(3)
            .And.Contain(g => g.GuildId == "guild-008")
            .And.Contain(g => g.GuildId == "guild-009")
            .And.Contain(g => g.GuildId == "guild-010");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoGuildsExist()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var all = await repo.GetAllAsync();

        // Assert
        all.Should().BeEmpty();
    }

    #endregion

    #region FindByMember Tests

    [Fact]
    public async Task FindByMemberAsync_ShouldReturnGuilds_WhenUserIsMember()
    {
        // Arrange
        var repo = CreateRepository();
        var guild1 = new Guild("guild-011", "creator-aaa", "User's Guild 1");
        guild1.AddMember("user-bbb", GuildRole.Member);
        await repo.CreateAsync(guild1);

        var guild2 = new Guild("guild-012", "creator-ccc", "User's Guild 2");
        guild2.AddMember("user-bbb", GuildRole.Admin);
        await repo.CreateAsync(guild2);

        var guild3 = new Guild("guild-013", "creator-ddd", "Other Guild");
        await repo.CreateAsync(guild3);

        // Act
        var userGuilds = await repo.FindByMemberAsync("user-bbb");

        // Assert
        userGuilds.Should().HaveCount(2)
            .And.Contain(g => g.GuildId == "guild-011")
            .And.Contain(g => g.GuildId == "guild-012")
            .And.NotContain(g => g.GuildId == "guild-013");
    }

    [Fact]
    public async Task FindByMemberAsync_ShouldIncludeGuildsWhereUserIsCreator()
    {
        // Arrange
        var repo = CreateRepository();
        var guild = new Guild("guild-014", "creator-eee", "Creator's Guild");
        await repo.CreateAsync(guild);

        // Act
        var userGuilds = await repo.FindByMemberAsync("creator-eee");

        // Assert
        userGuilds.Should().ContainSingle(g => g.GuildId == "guild-014");
    }

    [Fact]
    public async Task FindByMemberAsync_ShouldReturnEmptyList_WhenUserNotMemberOfAny()
    {
        // Arrange
        var repo = CreateRepository();
        await repo.CreateAsync(new Guild("guild-015", "creator-fff", "Some Guild"));

        // Act
        var userGuilds = await repo.FindByMemberAsync("nonmember-user");

        // Assert
        userGuilds.Should().BeEmpty();
    }

    #endregion
}
