using System;
using FluentAssertions;
using Game.Core.Domain;
using Xunit;

namespace Game.Core.Tests.Domain;

/// <summary>
/// TDD tests for GuildMember entity.
/// Coverage target: >= 90% lines, >= 85% branches.
/// </summary>
public class GuildMemberTests
{
    [Fact]
    public void Constructor_ShouldCreateGuildMemberWithValidParameters()
    {
        // Arrange
        var userId = "user-123";
        var role = GuildRole.Admin;

        // Act
        var member = new GuildMember(userId, role);

        // Assert
        member.UserId.Should().Be(userId);
        member.Role.Should().Be(role);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowException_WhenUserIdIsInvalid(string? invalidUserId)
    {
        // Arrange & Act
        var act = () => new GuildMember(invalidUserId!, GuildRole.Member);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId");
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenUserIdMatches()
    {
        // Arrange
        var member1 = new GuildMember("user-123", GuildRole.Member);
        var member2 = new GuildMember("user-123", GuildRole.Admin);

        // Act & Assert
        member1.Should().Be(member2, "GuildMember equality is based on UserId (Role is ignored)");
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenUserIdDiffers()
    {
        // Arrange
        var member1 = new GuildMember("user-123", GuildRole.Member);
        var member2 = new GuildMember("user-456", GuildRole.Member);

        // Act & Assert
        member1.Should().NotBe(member2, "different UserId values must not be equal");
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistent_ForSameUserId()
    {
        // Arrange
        var member1 = new GuildMember("user-123", GuildRole.Member);
        var member2 = new GuildMember("user-123", GuildRole.Admin);

        // Act & Assert
        member1.GetHashCode().Should().Be(member2.GetHashCode(),
            "same UserId should produce the same hash code");
    }
}
