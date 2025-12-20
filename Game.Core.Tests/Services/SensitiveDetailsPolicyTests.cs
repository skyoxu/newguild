using FluentAssertions;
using Game.Core.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace Game.Core.Tests.Services;

public class SensitiveDetailsPolicyTests
{
    private static Func<string, string?> EnvFrom(Dictionary<string, string?> map)
        => key => map.TryGetValue(key, out var v) ? v : null;

    [Theory]
    [InlineData(false, null, null, false)]
    [InlineData(false, "0", "1", false)]
    [InlineData(true, null, null, true)]
    [InlineData(true, "1", null, false)]
    [InlineData(true, "0", "1", false)]
    [InlineData(true, "1", "1", false)]
    [InlineData(true, "0", " ", true)]
    [InlineData(true, null, " ", true)]
    public void IncludeSensitiveDetails_RespectsEnvironment(
        bool isDebugBuild,
        string? gdSecureMode,
        string? ci,
        bool expected)
    {
        var env = new Dictionary<string, string?>
        {
            ["GD_SECURE_MODE"] = gdSecureMode,
            ["CI"] = ci,
        };

        var actual = SensitiveDetailsPolicy.IncludeSensitiveDetails(isDebugBuild, getEnv: EnvFrom(env));
        actual.Should().Be(expected);
    }

    [Fact]
    public void IncludeSensitiveDetails_WhenGetEnvNotProvided_ShouldMatchSystemEnvironment()
    {
        var expected =
            System.Environment.GetEnvironmentVariable("GD_SECURE_MODE") != "1"
            && string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("CI"));

        var actual = SensitiveDetailsPolicy.IncludeSensitiveDetails(isDebugBuild: true);
        actual.Should().Be(expected);
    }
}
