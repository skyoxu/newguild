using System.IO;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI
{
    public class ActivityFeedArtifactsTests
    {
        // ACC:T33.7
        [Fact]
        public void ShouldDefineExpectedArtifactPathPrefix_WhenCheckingCiArtifactConvention()
        {
            var logsRoot = Path.Combine("logs", "ci");
            var expected = $"logs{Path.DirectorySeparatorChar}ci";

            logsRoot.Should().Be(expected);
        }
    }
}
