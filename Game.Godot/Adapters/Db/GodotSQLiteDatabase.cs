using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using Game.Core.Domain;
using Game.Core.Ports;
using Game.Core.Services;
using Godot;
using Microsoft.Data.Sqlite;

namespace Game.Godot.Adapters.Db;

/// <summary>
/// Godot adapter for ISQLiteDatabase port interface.
/// Uses Microsoft.Data.Sqlite for Windows-only implementation (ADR-0011).
/// Follows ADR-0018 (adapter layer for Godot integration).
/// </summary>
public sealed class GodotSQLiteDatabase : ISQLiteDatabase, IDisposable
{
    private const string AuditLogFile = "security-audit.jsonl";
    private const string AuditSource = "GodotSQLiteDatabase";

    private SqliteConnection? _connection;
    private bool _isOpen;
    private readonly string _dbPathVirtual;
    private readonly string _dbPathAbsolute;
    private readonly string _auditLogPath;
    private long? _openedAtTicks;
    private int _queryCount;
    private int _scalarCount;
    private int _nonQueryCount;

    public GodotSQLiteDatabase(SafeResourcePath dbPath)
    {
        if (dbPath is null)
            throw new ArgumentNullException(nameof(dbPath));
        if (dbPath.Type != PathType.ReadWrite)
            throw new NotSupportedException("Only user:// paths are allowed for database files (ADR-0019)");

        _dbPathVirtual = dbPath.Value;
        _dbPathAbsolute = ProjectSettings.GlobalizePath(dbPath.Value);
        _auditLogPath = ResolveAuditLogPath();
    }

    public Task OpenAsync()
    {
        if (_isOpen) return Task.CompletedTask;

        ExecuteWithAudit(
            operation: "open",
            auditAction: "db.sqlite.open_failed",
            sql: null,
            action: () =>
            {
                var openStart = Stopwatch.GetTimestamp();
                // Ensure parent directory exists
                var dir = System.IO.Path.GetDirectoryName(_dbPathAbsolute);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = _dbPathAbsolute,
                    Mode = SqliteOpenMode.ReadWriteCreate
                }.ToString();

                _connection = new SqliteConnection(connectionString);
                _connection.Open();
                _isOpen = true;
                _openedAtTicks = Stopwatch.GetTimestamp();
                _queryCount = 0;
                _scalarCount = 0;
                _nonQueryCount = 0;

                if (ShouldAuditSuccess())
                {
                    var elapsedMs = ElapsedMs(openStart);
                    TryWriteAuditLog(
                        action: "db.sqlite.open_ok",
                        reason: "success",
                        target: _dbPathVirtual,
                        caller: AuditSource,
                        extra: new Dictionary<string, object?>
                        {
                            ["open_ms"] = elapsedMs,
                        });
                }
            });

        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        if (!_isOpen || _connection == null) return Task.CompletedTask;

        ExecuteWithAudit(
            operation: "close",
            auditAction: "db.sqlite.close_failed",
            sql: null,
            action: () =>
            {
                var durationMs = _openedAtTicks.HasValue ? ElapsedMs(_openedAtTicks.Value) : (double?)null;
                _connection.Close();
                _connection.Dispose();
                _connection = null;
                _isOpen = false;

                if (ShouldAuditSuccess())
                {
                    var extra = new Dictionary<string, object?>
                    {
                        ["duration_ms"] = durationMs,
                        ["query_count"] = _queryCount,
                        ["scalar_count"] = _scalarCount,
                        ["nonquery_count"] = _nonQueryCount,
                    };
                    TryWriteAuditLog(
                        action: "db.sqlite.close_ok",
                        reason: "success",
                        target: _dbPathVirtual,
                        caller: AuditSource,
                        extra: extra);
                }
            });

        return Task.CompletedTask;
    }

    public Task<int> ExecuteNonQueryAsync(SqlStatement stmt)
    {
        EnsureOpen();

        var sql = stmt.Text;
        var parameters = stmt.Parameters;

        var result = ExecuteWithAudit(
            operation: "nonquery",
            auditAction: "db.sqlite.nonquery_failed",
            sql: sql,
            func: () =>
            {
                using var command = _connection!.CreateCommand();
                command.CommandText = sql;
                ApplyCommandLimits(command);

                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }

                var rows = command.ExecuteNonQuery();
                _nonQueryCount++;
                return rows;
            });

        return Task.FromResult(result);
    }

    public Task<object?> ExecuteScalarAsync(SqlStatement stmt)
    {
        EnsureOpen();

        var sql = stmt.Text;
        var parameters = stmt.Parameters;

        var result = ExecuteWithAudit(
            operation: "scalar",
            auditAction: "db.sqlite.scalar_failed",
            sql: sql,
            func: () =>
            {
                using var command = _connection!.CreateCommand();
                command.CommandText = sql;
                ApplyCommandLimits(command);

                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                }

                var v = command.ExecuteScalar();
                _scalarCount++;
                return v;
            });

        return Task.FromResult(result == DBNull.Value ? null : result);
    }

    public Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(SqlStatement stmt)
    {
        EnsureOpen();

        var sql = stmt.Text;
        var parameters = stmt.Parameters;

        var result = ExecuteWithAudit(
            operation: "query",
            auditAction: "db.sqlite.query_failed",
            sql: sql,
            func: () =>
            {
                using var command = _connection!.CreateCommand();
                command.CommandText = sql;
                ApplyCommandLimits(command);

                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                }

                var results = new List<Dictionary<string, object>>();

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string columnName = reader.GetName(i);
                        object value = reader.GetValue(i);
                        row[columnName] = value == DBNull.Value ? null! : value;
                    }
                    results.Add(row);
                }

                _queryCount++;
                return (IReadOnlyList<Dictionary<string, object>>)results;
            });

        return Task.FromResult(result);
    }

    private static bool IncludeSensitiveDetails()
    {
#if DEBUG
        var isDebugBuild = true;
#else
        var isDebugBuild = false;
#endif

        return SensitiveDetailsPolicy.IncludeSensitiveDetails(isDebugBuild);
    }

    private string ResolveAuditLogPath()
    {
        var root = System.Environment.GetEnvironmentVariable("AUDIT_LOG_ROOT");
        if (!string.IsNullOrWhiteSpace(root))
            return Path.Combine(root, AuditLogFile);

        var isCi = !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("CI"));
        if (isCi)
        {
            var baseDir = ProjectSettings.GlobalizePath("res://");
            var rel = Path.Combine("logs", "ci", System.DateTime.UtcNow.ToString("yyyy-MM-dd"), AuditLogFile);
            return Path.GetFullPath(rel, baseDir);
        }

        return ProjectSettings.GlobalizePath(Path.Combine("user://logs/security", AuditLogFile));
    }

    private void TryWriteAuditLog(string action, string reason, string target, string caller, IReadOnlyDictionary<string, object?>? extra = null)
    {
        try
        {
            var directory = Path.GetDirectoryName(_auditLogPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var entry = new Dictionary<string, object?>
            {
                ["ts"] = System.DateTime.UtcNow.ToString("o"),
                ["action"] = action,
                ["reason"] = Truncate(reason, max: 500),
                ["target"] = Truncate(target, max: 1000),
                ["caller"] = caller,
            };
            if (extra != null)
            {
                foreach (var kv in extra)
                {
                    if (kv.Key == "ts" || kv.Key == "action" || kv.Key == "reason" || kv.Key == "target" || kv.Key == "caller")
                        continue;
                    entry[kv.Key] = kv.Value;
                }
            }

            var jsonLine = JsonSerializer.Serialize(entry) + System.Environment.NewLine;
            File.AppendAllText(_auditLogPath, jsonLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
            // Never throw from audit logging; keep primary error path deterministic.
        }
    }

    private static string BuildAuditReason(System.Exception ex)
    {
        if (ex is SqliteException sqliteEx)
        {
            return $"SqliteException code={sqliteEx.SqliteErrorCode}";
        }

        return ex.GetType().Name;
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        return value.Length <= max ? value : value.Substring(0, max);
    }

    private static double ElapsedMs(long startTimestamp)
    {
        var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        return elapsed * 1000.0 / Stopwatch.Frequency;
    }

    private static bool ShouldAuditSuccess()
    {
#if DEBUG
        var isDebugBuild = true;
#else
        var isDebugBuild = false;
#endif
        // Only emit success audit when sensitive details are disabled (CI/secure/release).
        return !SensitiveDetailsPolicy.IncludeSensitiveDetails(isDebugBuild);
    }

    private static void ApplyCommandLimits(SqliteCommand command)
    {
        // Configurable command timeout (seconds). Default: 30s in sanitized mode; unlimited in debug.
        // Note: Microsoft.Data.Sqlite uses CommandTimeout for ADO.NET timeouts.
        var timeoutEnv = System.Environment.GetEnvironmentVariable("GD_DB_COMMAND_TIMEOUT_SEC");
        if (!string.IsNullOrWhiteSpace(timeoutEnv) && int.TryParse(timeoutEnv, out var v))
        {
            if (v >= 0) command.CommandTimeout = v;
            return;
        }

        if (ShouldAuditSuccess())
            command.CommandTimeout = 30;
    }

    private void ExecuteWithAudit(string operation, string auditAction, string? sql, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            var includeSensitiveDetails = IncludeSensitiveDetails();
            if (!includeSensitiveDetails)
            {
                TryWriteAuditLog(
                    action: auditAction,
                    reason: BuildAuditReason(ex),
                    target: _dbPathVirtual,
                    caller: AuditSource);
            }
            throw DatabaseErrorHandling.CreateOperationException(operation, _dbPathVirtual, sql, ex, includeSensitiveDetails);
        }
    }

    private T ExecuteWithAudit<T>(string operation, string auditAction, string? sql, Func<T> func)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            var includeSensitiveDetails = IncludeSensitiveDetails();
            if (!includeSensitiveDetails)
            {
                TryWriteAuditLog(
                    action: auditAction,
                    reason: BuildAuditReason(ex),
                    target: _dbPathVirtual,
                    caller: AuditSource);
            }
            throw DatabaseErrorHandling.CreateOperationException(operation, _dbPathVirtual, sql, ex, includeSensitiveDetails);
        }
    }

    private void EnsureOpen()
    {
        if (!_isOpen || _connection == null)
            throw new InvalidOperationException("Database is not open. Call OpenAsync() first.");
    }

    public void Dispose()
    {
        try
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        catch
        {
            // Best-effort: Dispose must never throw.
        }
        finally
        {
            _connection = null;
            _isOpen = false;
        }
    }
}
