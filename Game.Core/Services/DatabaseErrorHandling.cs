using Game.Core.Contracts;
using System;

namespace Game.Core.Services;

public static class DatabaseErrorHandling
{
    public static InvalidOperationException CreateOperationException(
        string operation,
        string dbPath,
        string? sql,
        Exception ex,
        bool includeSensitiveDetails)
    {
        if (includeSensitiveDetails)
        {
            var op = string.IsNullOrWhiteSpace(operation) ? "operation" : operation.Trim();
            var message = $"Database operation failed ({op}). db={dbPath}; sql={sql}";
            return new InvalidOperationException(message, ex);
        }

        return new InvalidOperationException("Database operation failed.");
    }

    public static DomainEvent CreateAuditEvent(
        string operation,
        string dbPath,
        string? sql,
        Exception ex,
        string source)
    {
        var type = $"error.db.{ToEventSegment(operation)}.failed";
        var data = new
        {
            Operation = operation,
            DbPath = dbPath,
            Sql = sql,
            ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
            ExceptionMessage = ex.Message,
        };

        return new DomainEvent(
            Type: type,
            Source: source,
            Data: data,
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString());
    }

    private static string ToEventSegment(string operation)
    {
        var op = (operation ?? string.Empty).Trim().ToLowerInvariant();
        return op switch
        {
            "open" => "open",
            "close" => "close",
            "query" => "query",
            "scalar" => "scalar",
            "nonquery" => "nonquery",
            "non_query" => "nonquery",
            "execute_nonquery" => "nonquery",
            "execute_scalar" => "scalar",
            _ => "operation",
        };
    }
}

