using FluentAssertions;
using Game.Core.Contracts.Security;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class SecurityDemoGateDecisionContractsTests
{
    [Fact]
    public void EventTypeAndDecisions_ShouldHaveExpectedValues()
    {
        SecurityDemoGateDecision.EventType.Should().Be("core.security.raid_encounter_demo.decision");
        SecurityDemoGateDecision.DecisionAllow.Should().Be("allow");
        SecurityDemoGateDecision.DecisionDeny.Should().Be("deny");
        SecurityDemoGateDecision.DecisionError.Should().Be("error");
    }
}
