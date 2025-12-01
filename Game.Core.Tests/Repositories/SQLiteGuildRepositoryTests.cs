using Game.Core.Repositories;
using Game.Core.Tests.Mocks;

namespace Game.Core.Tests.Repositories;

/// <summary>
/// Concrete test class for SQLiteGuildRepository.
/// Inherits all contract tests from GuildRepositoryContractTests.
/// Uses MockSQLiteDatabase for isolated testing (ADR-0018).
/// Coverage target: ≥90% lines, ≥85% branches (ADR-0005).
/// </summary>
public class SQLiteGuildRepositoryTests : GuildRepositoryContractTests
{
    protected override IGuildRepository CreateRepository()
    {
        var mockDb = new MockSQLiteDatabase();
        return new SQLiteGuildRepository(mockDb);
    }
}
