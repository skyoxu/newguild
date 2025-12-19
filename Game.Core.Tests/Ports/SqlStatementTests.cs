using FluentAssertions;
using Game.Core.Ports;
using System;
using System.Collections.Generic;
using Xunit;

namespace Game.Core.Tests.Ports;

public class SqlStatementTests
{
    [Fact]
    public void NoParameters_ShouldRejectInlineStringLiterals()
    {
        Action act = () => SqlStatement.NoParameters("SELECT * FROM t WHERE name = 'x'");
        act.Should().Throw<ArgumentException>().WithMessage("*Inline string literals*");
    }

    [Fact]
    public void WithParameters_ShouldRequireAtLeastOneParameter()
    {
        Action act = () => SqlStatement.WithParameters(
            "SELECT * FROM t WHERE id = @Id",
            new Dictionary<string, object?>());

        act.Should().Throw<ArgumentException>().WithMessage("*at least one parameter*");
    }

    [Fact]
    public void WithParameters_ShouldRequireReferencedParameterNames()
    {
        Action act = () => SqlStatement.WithParameters(
            "SELECT * FROM t WHERE id = @Id",
            new Dictionary<string, object?> { ["@Other"] = 1 });

        act.Should().Throw<ArgumentException>().WithMessage("*does not reference parameter*");
    }

    [Fact]
    public void NoParameters_ShouldRejectSqlComments()
    {
        Action act = () => SqlStatement.NoParameters("SELECT 1 -- comment");
        act.Should().Throw<ArgumentException>().WithMessage("*comments are not allowed*");
    }

    [Fact]
    public void Positional_ShouldAllowContiguousParameters()
    {
        Action act = () => SqlStatement.Positional(
            "SELECT * FROM t WHERE a=@0 AND b=@1;",
            1,
            2);

        act.Should().NotThrow();
    }

    [Fact]
    public void Positional_ShouldRejectUnprovidedPositionalParameters()
    {
        Action act = () => SqlStatement.Positional(
            "SELECT * FROM t WHERE a=@1;",
            1);

        act.Should().Throw<ArgumentException>().WithMessage("*references positional parameter '@1'*");
    }

    [Fact]
    public void Positional_ShouldNotTreatAt1AsSubstringOfAt10()
    {
        Action act = () => SqlStatement.Positional(
            "SELECT * FROM t WHERE a=@0 AND b=@10;",
            new object?[11]);

        act.Should().Throw<ArgumentException>().WithMessage("*does not reference positional parameter '@1'*");
    }
}
