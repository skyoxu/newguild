using FluentAssertions;
using Game.Core.Contracts.Guild;
using Xunit;

namespace Game.Core.Tests.Contracts;

public class GuildOfficerRevokedContractTests
{
    [Fact]
    public void EventTypeConstant_ShouldMatchExpectedValue()
    {
        GuildOfficerRevoked.EventType.Should().Be("core.guild.officer.revoked");
    }

    [Fact]
    public void Contract_ShouldBeConstructible()
    {
        var evt = new GuildOfficerRevoked(
            GuildId: "g1",
            UserId: "u1",
            Slot: "raid_leader",
            RevokedAt: System.DateTimeOffset.UtcNow,
            RevokedByUserId: "admin1"
        );

        evt.GuildId.Should().Be("g1");
        evt.UserId.Should().Be("u1");
        evt.Slot.Should().Be("raid_leader");
        evt.RevokedByUserId.Should().Be("admin1");
    }
}
