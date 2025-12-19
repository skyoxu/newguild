using FluentAssertions;
using Game.Core.Services;
using System;
using Xunit;

namespace Game.Core.Tests.Services;

public class DatabaseErrorHandlingTests
{
    [Fact]
    public void CreateOperationException_WhenSensitiveDetailsDisabled_ShouldSanitizeMessageAndDropInnerException()
    {
        var sensitiveDbPath = @"C:\secret\game.db";
        var sensitiveSql = "SELECT * FROM users WHERE password = 'p@ss'";
        var inner = new InvalidOperationException("sqlite detailed failure");

        var ex = DatabaseErrorHandling.CreateOperationException(
            operation: "open",
            dbPath: sensitiveDbPath,
            sql: sensitiveSql,
            ex: inner,
            includeSensitiveDetails: false);

        ex.Message.Should().NotContain("secret");
        ex.Message.Should().NotContain("SELECT *");
        ex.Message.Should().NotContain("sqlite detailed failure");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void CreateOperationException_WhenSensitiveDetailsEnabled_ShouldIncludeDetailsAndInnerException()
    {
        var sensitiveDbPath = @"C:\secret\game.db";
        var sensitiveSql = "SELECT * FROM users";
        var inner = new InvalidOperationException("sqlite detailed failure");

        var ex = DatabaseErrorHandling.CreateOperationException(
            operation: "query",
            dbPath: sensitiveDbPath,
            sql: sensitiveSql,
            ex: inner,
            includeSensitiveDetails: true);

        ex.Message.Should().Contain("query");
        ex.Message.Should().Contain("secret");
        ex.Message.Should().Contain("SELECT *");
        ex.InnerException.Should().NotBeNull();
    }

    [Fact]
    public void CreateAuditEvent_ShouldIncludeSensitiveDetails()
    {
        var sensitiveDbPath = @"C:\secret\game.db";
        var sensitiveSql = "SELECT * FROM users";
        var inner = new InvalidOperationException("sqlite detailed failure");

        var evt = DatabaseErrorHandling.CreateAuditEvent(
            operation: "query",
            dbPath: sensitiveDbPath,
            sql: sensitiveSql,
            ex: inner,
            source: "GodotSQLiteDatabase");

        evt.Type.Should().StartWith("error.db.");
        evt.Source.Should().Be("GodotSQLiteDatabase");
        evt.Data.Should().NotBeNull();
        evt.Data!.ToString().Should().Contain("secret").And.Contain("SELECT *").And.Contain("sqlite detailed failure");
    }
}
