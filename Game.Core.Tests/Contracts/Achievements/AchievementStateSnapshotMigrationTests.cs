using FluentAssertions;
using Game.Core.Contracts.Achievements;
using Xunit;

namespace Game.Core.Tests.Contracts.Achievements;

public sealed class AchievementStateSnapshotMigrationTests
{
    [Fact]
    public void ShouldMigrateToCurrent_WhenSchemaVersionIsZero()
    {
        var migrated = AchievementStateSnapshotMigration.TryMigrateToCurrent(
            schemaVersion: 0,
            unlockedTriggerEventTypes: new[]
            {
                "core.guild.created",
                "core.guild.created",
                " core.media.beat.triggered ",
                "",
            },
            out var snapshot);

        migrated.Should().BeTrue();
        snapshot.SchemaVersion.Should().Be(AchievementStateSnapshot.CurrentSchemaVersion);
        snapshot.UnlockedCount.Should().Be(2);
        snapshot.UnlockedTriggerEventTypes.Should().Equal("core.guild.created", "core.media.beat.triggered");
    }

    [Fact]
    public void ShouldPreserveCurrentSchema_WhenSchemaVersionIsCurrent()
    {
        var migrated = AchievementStateSnapshotMigration.TryMigrateToCurrent(
            schemaVersion: AchievementStateSnapshot.CurrentSchemaVersion,
            unlockedTriggerEventTypes: new[] { "core.guild.created" },
            out var snapshot);

        migrated.Should().BeTrue();
        snapshot.SchemaVersion.Should().Be(AchievementStateSnapshot.CurrentSchemaVersion);
        snapshot.UnlockedCount.Should().Be(1);
        snapshot.UnlockedTriggerEventTypes.Should().Equal("core.guild.created");
    }

    [Fact]
    public void ShouldRejectMigration_WhenSchemaVersionIsUnsupported()
    {
        var migrated = AchievementStateSnapshotMigration.TryMigrateToCurrent(
            schemaVersion: 999,
            unlockedTriggerEventTypes: new[] { "core.guild.created" },
            out var snapshot);

        migrated.Should().BeFalse();
        snapshot.Should().Be(AchievementStateSnapshot.Empty);
    }
}

