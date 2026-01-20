using System;
using FluentAssertions;
using Game.Core.Contracts.Social;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class SocialRelationshipChangedDomainEventTests
{
    // ACC:T18.2
    [Fact]
    public void EventType_Should_Match_Expected()
    {
        SocialRelationshipChanged.EventType.Should().Be("core.social.relationship.changed");
    }

    // ACC:T18.2
    [Fact]
    public void Contract_Should_Accept_Valid_Fields()
    {
        var now = DateTimeOffset.UtcNow;
        _ = new SocialRelationshipChanged(
            GuildId: "guild-1",
            SubjectId: "m1",
            OtherId: "m2",
            OldValue: 1,
            NewValue: 2,
            ChangedAt: now);
    }
}
