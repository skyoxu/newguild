using Game.Core.Repositories;

namespace Game.Core.Tests.Repositories;

/// <summary>
/// Concrete test class for InMemoryGuildRepository.
/// Inherits all contract tests from GuildRepositoryContractTests.
/// Coverage target: ≥90% lines, ≥85% branches (ADR-0005).
/// </summary>
public class InMemoryGuildRepositoryTests : GuildRepositoryContractTests
{
    protected override IGuildRepository CreateRepository()
    {
        return new InMemoryGuildRepository();
    }
}
