using System;
using FluentAssertions;
using Game.Core.Observability;
using Xunit;

namespace Game.Core.Tests.CI
{
    public class ArtifactsLoggingTests
    {
        // ACC:T35.5
        [Fact]
        public void Should_BuildUnitArtifactPath_IncludeIsoDateSegment()
        {
            var stamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var path = ArtifactPathBuilder.BuildUnitArtifactPath(stamp, "artifact.json");

            path.Should().StartWith("logs/unit/2024-01-01/");
            path.Should().EndWith("artifact.json");
        }

        [Fact]
        public void Should_FormatUtcDate_ReturnIsoDate()
        {
            var stamp = ArtifactPathBuilder.FormatUtcDate(
                new DateTimeOffset(2024, 1, 1, 8, 30, 0, TimeSpan.FromHours(8)));

            stamp.Should().Be("2024-01-01");
        }

        [Fact]
        public void Should_Reject_ArtifactFileName_With_PathTraversal()
        {
            var act = () => ArtifactPathBuilder.BuildUnitArtifactPath(
                DateTimeOffset.UtcNow,
                "../artifact.json");

            act.Should()
                .Throw<ArgumentException>()
                .WithMessage("File name must not contain path separators or traversal.*");
        }
    }
}
