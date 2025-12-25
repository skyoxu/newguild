using FluentAssertions;
using System;
using System.Globalization;
using Xunit;

namespace Game.Core.Tests.Utilities;

public class RunDotnetCoverageGateDefaultsTests
{
    [Theory]
    [InlineData("COVERAGE_LINES_MIN")]
    [InlineData("COVERAGE_BRANCHES_MIN")]
    public void CoverageGateEnvVars_WhenProvided_MustBeValidPercent(string envVarName)
    {
        // Coverage gates are enforced by scripts/python/run_dotnet.py via environment variables.
        // This test intentionally avoids inspecting script source code; it only validates that
        // any provided values are parseable and within [0, 100].
        var raw = Environment.GetEnvironmentVariable(envVarName);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var ok = double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
        ok.Should().BeTrue($"{envVarName} must be a valid number (InvariantCulture), got: '{raw}'");
        value.Should().BeGreaterOrEqualTo(0);
        value.Should().BeLessOrEqualTo(100);
    }
}
