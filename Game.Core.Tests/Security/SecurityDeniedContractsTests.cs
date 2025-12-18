using FluentAssertions;
using Game.Core.Contracts.Security;
using Xunit;

namespace Game.Core.Tests.Security;

public class SecurityDeniedContractsTests
{
    [Fact]
    public void SecurityFileAccessDenied_ShouldExposeExpectedEventType()
    {
        SecurityFileAccessDenied.EventType.Should().Be("security.file_access.denied");
    }

    [Fact]
    public void SecurityProcessDenied_ShouldExposeExpectedEventType()
    {
        SecurityProcessDenied.EventType.Should().Be("security.process.denied");
    }

    [Fact]
    public void SecurityUrlAccessDenied_ShouldExposeExpectedEventType()
    {
        SecurityUrlAccessDenied.EventType.Should().Be("security.url_access.denied");
    }
}
