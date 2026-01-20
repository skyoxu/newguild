using FluentAssertions;
using Game.Core.Domain;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class IntimacyRulesBoundaryTests
{
    // ACC:T18.4
    [Fact]
    public void Should_Define_Consistent_Min_And_Max_Bounds()
    {
        IntimacyRules.MinIntimacy.Should().BeLessOrEqualTo(IntimacyRules.MaxIntimacy);
    }

    // ACC:T18.4
    [Fact]
    public void Clamp_Should_Enforce_Bounds()
    {
        IntimacyRules.Clamp(IntimacyRules.MinIntimacy - 1).Should().Be(IntimacyRules.MinIntimacy);
        IntimacyRules.Clamp(IntimacyRules.MaxIntimacy + 1).Should().Be(IntimacyRules.MaxIntimacy);
    }

    // ACC:T18.4
    [Fact]
    public void IsValidPeerPair_Should_Reject_SamePeer_And_EmptyIds()
    {
        IntimacyRules.IsValidPeerPair("", "x").Should().BeFalse();
        IntimacyRules.IsValidPeerPair("x", "").Should().BeFalse();
        IntimacyRules.IsValidPeerPair("x", "x").Should().BeFalse();
        IntimacyRules.IsValidPeerPair("x", "y").Should().BeTrue();
    }
}

