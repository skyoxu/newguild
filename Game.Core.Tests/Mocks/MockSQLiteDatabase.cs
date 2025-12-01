using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Core.Ports;

namespace Game.Core.Tests.Mocks;

/// <summary>
/// In-memory mock implementation of ISQLiteDatabase for testing.
/// Simulates SQLite behavior without actual database file.
/// Follows ADR-0018 (pure C# implementation, zero Godot dependencies).
/// </summary>
public class MockSQLiteDatabase : ISQLiteDatabase
{
    private readonly Dictionary<string, List<Dictionary<string, object>>> _tables = new();
    private bool _isOpen;

    public Task OpenAsync()
    {
        _isOpen = true;
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        _isOpen = false;
        return Task.CompletedTask;
    }

    public Task<int> ExecuteNonQueryAsync(string sql, Dictionary<string, object>? parameters = null)
    {
        EnsureOpen();

        sql = sql.Trim();

        if (sql.StartsWith("CREATE TABLE IF NOT EXISTS", StringComparison.OrdinalIgnoreCase))
        {
            var tableName = ExtractTableName(sql);
            if (!_tables.ContainsKey(tableName))
            {
                _tables[tableName] = new List<Dictionary<string, object>>();
            }
            return Task.FromResult(0);
        }

        if (sql.StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ExecuteInsert(sql, parameters));
        }

        if (sql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ExecuteUpdate(sql, parameters));
        }

        if (sql.StartsWith("DELETE FROM", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ExecuteDelete(sql, parameters));
        }

        return Task.FromResult(0);
    }

    public Task<object?> ExecuteScalarAsync(string sql, Dictionary<string, object>? parameters = null)
    {
        EnsureOpen();

        var rows = ExecuteQuery(sql, parameters);
        if (rows.Count == 0 || rows[0].Count == 0)
            return Task.FromResult<object?>(null);

        return Task.FromResult<object?>(rows[0].Values.First());
    }

    public Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(string sql, Dictionary<string, object>? parameters = null)
    {
        EnsureOpen();

        var rows = ExecuteQuery(sql, parameters);
        return Task.FromResult<IReadOnlyList<Dictionary<string, object>>>(rows);
    }

    private void EnsureOpen()
    {
        if (!_isOpen)
            throw new InvalidOperationException("Database is not open");
    }

    private string ExtractTableName(string sql)
    {
        // Simple extraction: "CREATE TABLE IF NOT EXISTS TableName ..."
        var parts = sql.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var index = Array.FindIndex(parts, p => p.Equals("EXISTS", StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < parts.Length ? parts[index + 1] : parts[2];
    }

    private int ExecuteInsert(string sql, Dictionary<string, object>? parameters)
    {
        // Extract table name: "INSERT INTO TableName (...) VALUES (...)"
        var tableName = sql.Substring(sql.IndexOf("INTO") + 4).Split('(')[0].Trim();

        if (!_tables.ContainsKey(tableName))
            _tables[tableName] = new List<Dictionary<string, object>>();

        var row = new Dictionary<string, object>();
        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                var key = kvp.Key.TrimStart('@');
                row[key] = kvp.Value;
            }
        }

        _tables[tableName].Add(row);
        return 1;
    }

    private int ExecuteUpdate(string sql, Dictionary<string, object>? parameters)
    {
        // Extract table name: "UPDATE TableName SET ..."
        var tableName = sql.Substring(sql.IndexOf("UPDATE") + 6).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();

        if (!_tables.ContainsKey(tableName))
            return 0;

        var whereClause = ExtractWhereClause(sql, parameters);
        var matchingRows = _tables[tableName].Where(row => MatchesWhere(row, whereClause)).ToList();

        foreach (var row in matchingRows)
        {
            if (parameters != null)
            {
                foreach (var kvp in parameters)
                {
                    var key = kvp.Key.TrimStart('@');
                    if (row.ContainsKey(key))
                        row[key] = kvp.Value;
                }
            }
        }

        return matchingRows.Count;
    }

    private int ExecuteDelete(string sql, Dictionary<string, object>? parameters)
    {
        // Extract table name: "DELETE FROM TableName WHERE ..."
        var tableName = sql.Substring(sql.IndexOf("FROM") + 4).Split(new[] { ' ', 'W' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();

        if (!_tables.ContainsKey(tableName))
            return 0;

        var whereClause = ExtractWhereClause(sql, parameters);
        var matchingRows = _tables[tableName].Where(row => MatchesWhere(row, whereClause)).ToList();

        foreach (var row in matchingRows)
        {
            _tables[tableName].Remove(row);
        }

        return matchingRows.Count;
    }

    private List<Dictionary<string, object>> ExecuteQuery(string sql, Dictionary<string, object>? parameters)
    {
        // Extract table name from SELECT query
        string tableName;
        if (sql.Contains("FROM"))
        {
            var fromIndex = sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase) + 4;
            var afterFrom = sql.Substring(fromIndex).Trim();

            // Handle JOIN queries
            if (afterFrom.Contains("INNER JOIN", StringComparison.OrdinalIgnoreCase))
            {
                tableName = afterFrom.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
            }
            else if (afterFrom.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
            {
                tableName = afterFrom.Substring(0, afterFrom.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase)).Trim();
            }
            else
            {
                tableName = afterFrom.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
            }
        }
        else
        {
            return new List<Dictionary<string, object>>();
        }

        // Remove alias if present (e.g., "Guilds g" -> "Guilds")
        tableName = tableName.Split(' ')[0];

        if (!_tables.ContainsKey(tableName))
            return new List<Dictionary<string, object>>();

        var whereClause = ExtractWhereClause(sql, parameters);
        var rows = _tables[tableName].Where(row => MatchesWhere(row, whereClause)).ToList();

        // Handle JOINs
        if (sql.Contains("INNER JOIN", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteJoinQuery(sql, parameters);
        }

        return rows;
    }

    private List<Dictionary<string, object>> ExecuteJoinQuery(string sql, Dictionary<string, object>? parameters)
    {
        // Simplified JOIN handling for GuildMembers query
        if (sql.Contains("GuildMembers") && parameters != null && parameters.ContainsKey("@UserId"))
        {
            var userId = (string)parameters["@UserId"];
            var guildMembers = _tables.GetValueOrDefault("GuildMembers") ?? new List<Dictionary<string, object>>();
            var guilds = _tables.GetValueOrDefault("Guilds") ?? new List<Dictionary<string, object>>();

            var matchingGuildIds = guildMembers
                .Where(m => m["UserId"].ToString() == userId)
                .Select(m => m["GuildId"].ToString())
                .ToHashSet();

            return guilds.Where(g => matchingGuildIds.Contains(g["GuildId"].ToString())).ToList();
        }

        return new List<Dictionary<string, object>>();
    }

    private Dictionary<string, object> ExtractWhereClause(string sql, Dictionary<string, object>? parameters)
    {
        var result = new Dictionary<string, object>();

        if (!sql.Contains("WHERE", StringComparison.OrdinalIgnoreCase) || parameters == null)
            return result;

        foreach (var kvp in parameters)
        {
            result[kvp.Key.TrimStart('@')] = kvp.Value;
        }

        return result;
    }

    private bool MatchesWhere(Dictionary<string, object> row, Dictionary<string, object> whereClause)
    {
        if (whereClause.Count == 0)
            return true;

        foreach (var kvp in whereClause)
        {
            if (!row.ContainsKey(kvp.Key))
                return false;

            if (!row[kvp.Key].ToString()!.Equals(kvp.Value.ToString()))
                return false;
        }

        return true;
    }
}
