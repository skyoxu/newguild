using FluentAssertions;
using Game.Core.Contracts.Security;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class SecurityDemoGateDecisionContractsTests
{
    [Fact]
    public void EventType_and_decisions_have_expected_values()
    {
        SecurityDemoGateDecision.EventType.Should().Be("security.raid_encounter_demo.decision");
        SecurityDemoGateDecision.DecisionAllow.Should().Be("allow");
        SecurityDemoGateDecision.DecisionDeny.Should().Be("deny");
        SecurityDemoGateDecision.DecisionError.Should().Be("error");
    }
}

