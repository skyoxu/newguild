using System;
using System.Text.Json;
using FluentAssertions;
using Game.Core.Contracts.Progression;
using Game.Core.Progression;
using Xunit;

namespace Game.Core.Tests.Progression;

public sealed class ExperienceSnapshotNormalizerTests
{
    [Fact]
    public void Should_Normalize_Canonical_Payload()
    {
        var rawPayload = "{" +
                         "\"guildId\":\"guild-1\"," +
                         "\"totalExperience\":150," +
                         "\"delta\":10," +
                         "\"level\":2," +
                         "\"sourceEventType\":\"core.raid.resolved\"," +
                         "\"changedAt\":\"2025-01-01T00:00:00Z\"" +
                         "}";

        var ok = ExperienceSnapshotNormalizer.TryNormalize(rawPayload, out var normalizedPayload);

        ok.Should().BeTrue();
        using var document = JsonDocument.Parse(normalizedPayload);
        document.RootElement.GetProperty("guildId").GetString().Should().Be("guild-1");
        document.RootElement.GetProperty("totalExperience").GetInt32().Should().Be(150);
        document.RootElement.GetProperty("level").GetInt32().Should().Be(2);
        document.RootElement.GetProperty("sourceEventType").GetString().Should().Be("core.raid.resolved");
        document.RootElement.GetProperty("changedAt").GetDateTimeOffset()
            .Should().Be(DateTimeOffset.Parse("2025-01-01T00:00:00+00:00"));
    }

    [Fact]
    public void Should_Reject_Legacy_UserId_Payload()
    {
        var rawPayload = "{" +
                         "\"userId\":\"user-1\"," +
                         "\"total\":99," +
                         "\"newLevel\":3," +
                         "\"changedAt\":\"2025-01-01T00:00:01Z\"" +
                         "}";

        var ok = ExperienceSnapshotNormalizer.TryNormalize(rawPayload, out var normalizedPayload);

        ok.Should().BeFalse();
        normalizedPayload.Should().BeEmpty();
    }

    [Fact]
    public void Should_Normalize_GuildId_With_Fallback_Total_And_NewLevel()
    {
        var rawPayload = "{" +
                         "\"guildId\":\"guild-fallback\"," +
                         "\"total\":77," +
                         "\"newLevel\":4," +
                         "\"sourceEventType\":\"ui.invalid\"," +
                         "\"changedAt\":\"2025-01-01T00:00:01Z\"" +
                         "}";

        var ok = ExperienceSnapshotNormalizer.TryNormalize(rawPayload, out var normalizedPayload);

        ok.Should().BeTrue();
        using var document = JsonDocument.Parse(normalizedPayload);
        document.RootElement.GetProperty("guildId").GetString().Should().Be("guild-fallback");
        document.RootElement.GetProperty("totalExperience").GetInt32().Should().Be(77);
        document.RootElement.GetProperty("level").GetInt32().Should().Be(4);
        document.RootElement.GetProperty("sourceEventType").GetString().Should().Be(ExperienceChanged.EventType);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("[]")]
    [InlineData("1")]
    [InlineData("{\"totalExperience\":1,\"level\":1}")]
    [InlineData("{\"guildId\":\"g\",\"totalExperience\":-1,\"level\":1}")]
    [InlineData("{\"guildId\":\"g\",\"totalExperience\":1,\"level\":0}")]
    public void Should_Reject_Invalid_Payload(string rawPayload)
    {
        var ok = ExperienceSnapshotNormalizer.TryNormalize(rawPayload, out var normalizedPayload);

        ok.Should().BeFalse();
        normalizedPayload.Should().BeEmpty();
    }

    [Fact]
    public void Should_Reject_Oversized_Payload()
    {
        var oversizedPayload = "{" + "\"guildId\":\"" + new string('a', 8200) + "\",\"totalExperience\":1,\"level\":1}";

        var ok = ExperienceSnapshotNormalizer.TryNormalize(oversizedPayload, out _);

        ok.Should().BeFalse();
    }

    [Fact]
    public void Should_Reject_Invalid_ChangedAt()
    {
        var rawPayload = "{" +
                         "\"guildId\":\"guild-2\"," +
                         "\"totalExperience\":42," +
                         "\"delta\":1," +
                         "\"level\":2," +
                         "\"changedAt\":\"invalid\"" +
                         "}";

        var ok = ExperienceSnapshotNormalizer.TryNormalize(rawPayload, out var normalizedPayload);

        ok.Should().BeFalse();
        normalizedPayload.Should().BeEmpty();
    }

    [Fact]
    public void Should_Keep_ChangedAt_Stable_When_Normalized_Twice()
    {
        var rawPayload = "{" +
                         "\"guildId\":\"guild-3\"," +
                         "\"totalExperience\":88," +
                         "\"delta\":5," +
                         "\"level\":3," +
                         "\"changedAt\":\"2025-01-01T00:00:00+00:00\"" +
                         "}";

        var firstOk = ExperienceSnapshotNormalizer.TryNormalize(rawPayload, out var firstNormalizedPayload);
        var secondOk = ExperienceSnapshotNormalizer.TryNormalize(firstNormalizedPayload, out var secondNormalizedPayload);

        firstOk.Should().BeTrue();
        secondOk.Should().BeTrue();

        using var firstDocument = JsonDocument.Parse(firstNormalizedPayload);
        using var secondDocument = JsonDocument.Parse(secondNormalizedPayload);
        var firstChangedAt = firstDocument.RootElement.GetProperty("changedAt").GetDateTimeOffset();
        var secondChangedAt = secondDocument.RootElement.GetProperty("changedAt").GetDateTimeOffset();
        secondChangedAt.Should().Be(firstChangedAt);
    }
}
