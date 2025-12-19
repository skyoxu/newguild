using FluentAssertions;
using Game.Core.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace Game.Core.Tests.Services;

public class SensitiveDetailsPolicyTests
{
    [Fact]
    public void IncludeSensitiveDetails_WhenNotDebugBuild_ReturnsFalse()
    {
        SensitiveDetailsPolicy.IncludeSensitiveDetails(isDebugBuild: false, _ => null).Should().BeFalse();
    }

    [Fact]
    public void IncludeSensitiveDetails_WhenDebugBuildAndNoEnvFlags_ReturnsTrue()
    {
        SensitiveDetailsPolicy.IncludeSensitiveDetails(isDebugBuild: true, _ => null).Should().BeTrue();
    }

    [Fact]
    public void IncludeSensitiveDetails_WhenSecureModeEnabled_ReturnsFalse()
    {
        var env = new Dictionary<string, string?> { ["GD_SECURE_MODE"] = "1" };
        SensitiveDetailsPolicy.IncludeSensitiveDetails(isDebugBuild: true, k => env.GetValueOrDefault(k)).Should().BeFalse();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("YES")]
    public void IncludeSensitiveDetails_WhenCiEnvVarIsSet_ReturnsFalse(string ciValue)
    {
        var env = new Dictionary<string, string?> { ["CI"] = ciValue };
        SensitiveDetailsPolicy.IncludeSensitiveDetails(isDebugBuild: true, k => env.GetValueOrDefault(k)).Should().BeFalse();
    }

    [Fact]
    public void IncludeSensitiveDetails_WhenCiEnvVarIsWhitespace_ReturnsTrue()
    {
        var env = new Dictionary<string, string?> { ["CI"] = "   " };
        SensitiveDetailsPolicy.IncludeSensitiveDetails(isDebugBuild: true, k => env.GetValueOrDefault(k)).Should().BeTrue();
    }
}

