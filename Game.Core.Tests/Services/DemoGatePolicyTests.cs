using FluentAssertions;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Services;

public sealed class DemoGatePolicyTests
{
    [Fact]
    public void Should_Disable_Demos_When_PlayableOverride_Is_False_Even_In_Debug()
    {
        var enabled = DemoGatePolicy.AreDemosEnabled(playableOverride: false, securityTestModeEnabled: true, isDebugBuild: true);
        enabled.Should().BeFalse();
    }

    [Fact]
    public void Should_Enable_Demos_When_PlayableOverride_Is_True()
    {
        var enabled = DemoGatePolicy.AreDemosEnabled(playableOverride: true, securityTestModeEnabled: false, isDebugBuild: false);
        enabled.Should().BeTrue();
    }

    [Fact]
    public void Should_Enable_Demos_In_Debug_When_PlayableOverride_Is_Null()
    {
        var enabled = DemoGatePolicy.AreDemosEnabled(playableOverride: null, securityTestModeEnabled: false, isDebugBuild: true);
        enabled.Should().BeTrue();
    }

    [Fact]
    public void Should_Enable_Demos_In_Release_When_Security_Test_Mode_Is_Enabled()
    {
        var enabled = DemoGatePolicy.AreDemosEnabled(playableOverride: null, securityTestModeEnabled: true, isDebugBuild: false);
        enabled.Should().BeTrue();
    }

    [Fact]
    public void Should_Disable_Demos_In_Release_When_No_Override_Is_Set()
    {
        var enabled = DemoGatePolicy.AreDemosEnabled(playableOverride: null, securityTestModeEnabled: false, isDebugBuild: false);
        enabled.Should().BeFalse();
    }
}
