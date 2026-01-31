using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Domain;
using Game.Core.Repositories;
using Game.Core.Tests.Mocks;
using Xunit;

namespace Game.Core.Tests.Repositories;

public class SQLiteGuildRepositorySaveAsyncTests
{
    [Fact]
    public async Task Should_Throw_When_SaveAsync_Guild_Is_Null()
    {
        var repo = new SQLiteGuildRepository(new MockSQLiteDatabase());

        Func<Task> act = async () => await repo.SaveAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Should_Throw_When_SaveAsync_Cancelled()
    {
        var repo = new SQLiteGuildRepository(new MockSQLiteDatabase());
        var guild = new Guild("g1", "creator-1", "Guild");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await repo.SaveAsync(guild, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
