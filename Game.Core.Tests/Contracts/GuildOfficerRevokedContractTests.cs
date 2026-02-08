using FluentAssertions;
using Game.Core.Contracts.Guild;
using Xunit;

namespace Game.Core.Tests.Contracts;

public class GuildOfficerRevokedContractTests
{
    [Fact]
    public void EventType_constant_is_expected()
    {
        GuildOfficerRevoked.EventType.Should().Be("core.guild.officer.revoked");
    }

    [Fact]
    public void Contract_is_constructible()
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

