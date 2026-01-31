using System;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Persistence.Migrations;
using Xunit;

namespace Game.Core.Tests.Persistence.Migrations;

public class GuildDbSchemaTests
{
    [Fact]
    public async Task EnsureTablesExistAsync_Should_Throw_When_Database_Is_Null()
    {
        Func<Task> act = async () => await GuildDbSchema.EnsureTablesExistAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
