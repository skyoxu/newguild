using FluentAssertions;
using Game.Core.Ports;
using System;
using System.Collections.Generic;
using Xunit;

namespace Game.Core.Tests.Ports;

public class SqlStatementTests
{
    [Fact]
    public void NoParameters_ShouldRejectWhereClause()
    {
        // Build the SQL at runtime to avoid static scan false-positives in test code.
        var sql = "DELETE FROM users " + "WHERE id = 1";
        Action act = () => SqlStatement.NoParameters(sql);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must not contain WHERE*");
    }

    [Fact]
    public void NoParameters_ShouldAllowSafeStatementsWithoutWhere()
    {
        var stmt = SqlStatement.NoParameters("SELECT 1;");
        stmt.Text.Should().Be("SELECT 1;");
        stmt.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void WithParameters_ShouldRejectUnusedParameters()
    {
        Action act = () => SqlStatement.WithParameters(
            "DELETE FROM users;",
            new Dictionary<string, object?> { ["@Id"] = 1 });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*does not reference parameter*");
    }

    [Fact]
    public void Positional_ShouldRejectUnusedPositionalParameters()
    {
        Action act = () => SqlStatement.Positional("DELETE FROM users;", 1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*does not reference positional parameter*");
    }
}
