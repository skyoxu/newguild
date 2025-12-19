using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Ports;

public sealed record SqlStatement
{
    public string Text { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    private SqlStatement(string text, IReadOnlyDictionary<string, object?> parameters)
    {
        Text = text;
        Parameters = parameters;
    }

    public static SqlStatement NoParameters(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL cannot be empty.", nameof(sql));

        var trimmed = sql.Trim();
        EnsureNoCommentsOrMultipleStatements(trimmed);
        EnsureNoStringLiterals(trimmed);

        return new SqlStatement(trimmed, EmptyParameters.Instance);
    }

    public static SqlStatement WithParameters(string sql, IReadOnlyDictionary<string, object?> parameters)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL cannot be empty.", nameof(sql));
        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));
        if (parameters.Count == 0)
            throw new ArgumentException("Parameterized SQL requires at least one parameter.", nameof(parameters));

        var trimmed = sql.Trim();
        EnsureNoCommentsOrMultipleStatements(trimmed);
        EnsureNoStringLiterals(trimmed);

        foreach (var key in parameters.Keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Parameter name cannot be empty.", nameof(parameters));
            if (!key.StartsWith("@", StringComparison.Ordinal))
                throw new ArgumentException($"Parameter name must start with '@': {key}", nameof(parameters));
            if (!trimmed.Contains(key, StringComparison.Ordinal))
                throw new ArgumentException($"SQL text does not reference parameter '{key}'.", nameof(parameters));
        }

        return new SqlStatement(trimmed, parameters);
    }

    public static SqlStatement Positional(string sql, params object?[] parameters)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL cannot be empty.", nameof(sql));
        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));
        if (parameters.Length == 0)
            throw new ArgumentException("Positional SQL requires at least one parameter.", nameof(parameters));

        var trimmed = sql.Trim();
        EnsureNoCommentsOrMultipleStatements(trimmed);
        EnsureNoStringLiterals(trimmed);

        var usage = GetPositionalParameterUsage(trimmed, parameters.Length);
        for (var i = 0; i < parameters.Length; i++)
        {
            if (!usage[i])
                throw new ArgumentException($"SQL text does not reference positional parameter '@{i}'.", nameof(sql));
        }

        var map = new Dictionary<string, object?>(capacity: parameters.Length, comparer: StringComparer.Ordinal);
        for (var i = 0; i < parameters.Length; i++)
        {
            var key = $"@{i}";
            map[key] = parameters[i];
        }

        return new SqlStatement(trimmed, map);
    }

    private static bool[] GetPositionalParameterUsage(string sql, int parameterCount)
    {
        var used = new bool[parameterCount];
        for (var i = 0; i < sql.Length; i++)
        {
            if (sql[i] != '@') continue;
            if (i + 1 >= sql.Length || !char.IsDigit(sql[i + 1])) continue;

            var j = i + 1;
            var value = 0;
            while (j < sql.Length && char.IsDigit(sql[j]))
            {
                checked { value = (value * 10) + (sql[j] - '0'); }
                j++;
            }

            if (value >= parameterCount)
                throw new ArgumentException($"SQL references positional parameter '@{value}' but only {parameterCount} parameter(s) were provided.", nameof(sql));

            used[value] = true;

            i = j - 1;
        }

        return used;
    }

    private static void EnsureNoCommentsOrMultipleStatements(string sql)
    {
        if (sql.Contains("--", StringComparison.Ordinal) ||
            sql.Contains("/*", StringComparison.Ordinal) ||
            sql.Contains("*/", StringComparison.Ordinal))
            throw new ArgumentException("SQL comments are not allowed.", nameof(sql));

        var idx = sql.IndexOf(';');
        if (idx >= 0 && idx != sql.Length - 1)
            throw new ArgumentException("Multiple SQL statements are not allowed.", nameof(sql));
    }

    private static void EnsureNoStringLiterals(string sql)
    {
        if (sql.Contains('\''))
            throw new ArgumentException("Inline string literals are not allowed; use parameters instead.", nameof(sql));
        if (sql.Contains('\"'))
            throw new ArgumentException("Inline quoted identifiers are not allowed; use unquoted identifiers.", nameof(sql));
    }

    private sealed class EmptyParameters : IReadOnlyDictionary<string, object?>
    {
        public static readonly EmptyParameters Instance = new();

        public int Count => 0;
        public IEnumerable<string> Keys => Enumerable.Empty<string>();
        public IEnumerable<object?> Values => Enumerable.Empty<object?>();
        public object? this[string key] => throw new KeyNotFoundException();
        public bool ContainsKey(string key) => false;
        public bool TryGetValue(string key, out object? value) { value = null; return false; }
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => Enumerable.Empty<KeyValuePair<string, object?>>().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
