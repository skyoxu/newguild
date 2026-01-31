using System;
using FluentAssertions;
using Game.Core.Ports;
using Game.Core.Repositories;
using Xunit;

namespace Game.Core.Tests.Repositories;

public class SQLiteGuildRepositoryGuardTests
{
    [Fact]
    public void Should_Throw_When_Database_Is_Null()
    {
        Action act = () => new SQLiteGuildRepository((ISQLiteDatabase)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Should_Throw_When_DbPath_Is_Empty()
    {
        Action act = () => new SQLiteGuildRepository("   ");

        act.Should().Throw<ArgumentException>();
    }
}
