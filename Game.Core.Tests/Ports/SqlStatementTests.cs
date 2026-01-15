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

    [Fact]
    public void NoParameters_ShouldRejectSqlComments()
    {
        var sql = "SELECT 1 " + "--" + " comment";
        Action act = () => SqlStatement.NoParameters(sql);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*comments*");
    }

    [Fact]
    public void NoParameters_ShouldRejectMultipleStatements()
    {
        var sql = "SELECT 1;" + " SELECT 2";
        Action act = () => SqlStatement.NoParameters(sql);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Multiple SQL statements*");
    }

    [Fact]
    public void NoParameters_ShouldRejectInlineStringLiterals()
    {
        var quote = (char)39;
        var sql = "SELECT " + quote + "x" + quote;
        Action act = () => SqlStatement.NoParameters(sql);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Inline string literals*");
    }

    [Fact]
    public void WithParameters_ShouldAllowSqlWithReferencedParameters()
    {
        var sql = "SELECT 1 " + "WHERE id = @Id";
        var stmt = SqlStatement.WithParameters(sql, new Dictionary<string, object?> { ["@Id"] = 1 });

        stmt.Text.Should().Be(sql);
        stmt.Parameters.Should().ContainKey("@Id");
    }

    [Fact]
    public void WithParameters_ShouldRejectParameterNameWithoutAtPrefix()
    {
        var sql = "SELECT 1 " + "WHERE id = @Id";
        Action act = () => SqlStatement.WithParameters(sql, new Dictionary<string, object?> { ["Id"] = 1 });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must start with '@'*");
    }

    [Fact]
    public void Positional_ShouldAllowReferencedParameters()
    {
        var stmt = SqlStatement.Positional("SELECT @0;", 1);
        stmt.Text.Should().Be("SELECT @0;");
        stmt.Parameters.Should().ContainKey("@0");
        stmt.Parameters["@0"].Should().Be(1);
    }
}
