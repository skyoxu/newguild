using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Text.Json;
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
public partial class GodotSQLiteDatabase : Node, ISQLiteDatabase
{
    private const string AuditLogFile = "security-audit.jsonl";
    private const string AuditSource = "GodotSQLiteDatabase";

    private SqliteConnection? _connection;
    private bool _isOpen;
    private readonly string _dbPathVirtual;
    private readonly string _dbPathAbsolute;
    private readonly string _auditLogPath;

    public GodotSQLiteDatabase(string dbPath = "user://game.db")
    {
        // Security: enforce SafeResourcePath for user:// only (ADR-0019)
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("Database path cannot be empty", nameof(dbPath));

        var safePath = SafeResourcePath.FromString(dbPath);
        if (safePath == null || safePath.Type != PathType.ReadWrite)
            throw new NotSupportedException("Only user:// paths are allowed for database files (ADR-0019)");

        _dbPathVirtual = dbPath;
        _dbPathAbsolute = ProjectSettings.GlobalizePath(dbPath);
        _auditLogPath = ResolveAuditLogPath();
    }

    public Task OpenAsync()
    {
        if (_isOpen) return Task.CompletedTask;

        try
        {
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

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            var includeSensitiveDetails = IncludeSensitiveDetails();
            if (!includeSensitiveDetails)
            {
                TryWriteAuditLog(
                    action: "db.sqlite.open_failed",
                    reason: BuildAuditReason(ex),
                    target: _dbPathVirtual,
                    caller: AuditSource);
            }

            throw DatabaseErrorHandling.CreateOperationException("open", _dbPathVirtual, null, ex, includeSensitiveDetails);
        }
    }

    public Task CloseAsync()
    {
        if (!_isOpen || _connection == null) return Task.CompletedTask;

        try
        {
            _connection.Close();
            _connection.Dispose();
            _connection = null;
            _isOpen = false;

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            var includeSensitiveDetails = IncludeSensitiveDetails();
            if (!includeSensitiveDetails)
            {
                TryWriteAuditLog(
                    action: "db.sqlite.close_failed",
                    reason: BuildAuditReason(ex),
                    target: _dbPathVirtual,
                    caller: AuditSource);
            }

            throw DatabaseErrorHandling.CreateOperationException("close", _dbPathVirtual, null, ex, includeSensitiveDetails);
        }
    }

    public Task<int> ExecuteNonQueryAsync(SqlStatement stmt)
    {
        EnsureOpen();

        var sql = stmt.Text;
        var parameters = stmt.Parameters;

        try
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = sql;

            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }

            int result = command.ExecuteNonQuery();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            var includeSensitiveDetails = IncludeSensitiveDetails();
            if (!includeSensitiveDetails)
            {
                TryWriteAuditLog(
                    action: "db.sqlite.nonquery_failed",
                    reason: BuildAuditReason(ex),
                    target: _dbPathVirtual,
                    caller: AuditSource);
            }

            throw DatabaseErrorHandling.CreateOperationException("nonquery", _dbPathVirtual, sql, ex, includeSensitiveDetails);
        }
    }

    public Task<object?> ExecuteScalarAsync(SqlStatement stmt)
    {
        EnsureOpen();

        var sql = stmt.Text;
        var parameters = stmt.Parameters;

        try
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = sql;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }
            }

            object? result = command.ExecuteScalar();
            return Task.FromResult(result == DBNull.Value ? null : result);
        }
        catch (Exception ex)
        {
            var includeSensitiveDetails = IncludeSensitiveDetails();
            if (!includeSensitiveDetails)
            {
                TryWriteAuditLog(
                    action: "db.sqlite.scalar_failed",
                    reason: BuildAuditReason(ex),
                    target: _dbPathVirtual,
                    caller: AuditSource);
            }

            throw DatabaseErrorHandling.CreateOperationException("scalar", _dbPathVirtual, sql, ex, includeSensitiveDetails);
        }
    }

    public Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(SqlStatement stmt)
    {
        EnsureOpen();

        var sql = stmt.Text;
        var parameters = stmt.Parameters;

        try
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = sql;

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

            return Task.FromResult<IReadOnlyList<Dictionary<string, object>>>(results);
        }
        catch (Exception ex)
        {
            var includeSensitiveDetails = IncludeSensitiveDetails();
            if (!includeSensitiveDetails)
            {
                TryWriteAuditLog(
                    action: "db.sqlite.query_failed",
                    reason: BuildAuditReason(ex),
                    target: _dbPathVirtual,
                    caller: AuditSource);
            }

            throw DatabaseErrorHandling.CreateOperationException("query", _dbPathVirtual, sql, ex, includeSensitiveDetails);
        }
    }

    private static bool IncludeSensitiveDetails()
    {
        var isSecureMode = System.Environment.GetEnvironmentVariable("GD_SECURE_MODE") == "1";
        var isCi = !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("CI"));

#if DEBUG
        var isDebugBuild = true;
#else
        var isDebugBuild = false;
#endif

        if (!isDebugBuild)
            return false;

        return !isSecureMode && !isCi;
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

    private void TryWriteAuditLog(string action, string reason, string target, string caller)
    {
        try
        {
            var directory = Path.GetDirectoryName(_auditLogPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var entry = new
            {
                ts = System.DateTime.UtcNow.ToString("o"),
                action,
                reason = Truncate(reason, max: 500),
                target = Truncate(target, max: 1000),
                caller,
            };

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

    private void EnsureOpen()
    {
        if (!_isOpen || _connection == null)
            throw new InvalidOperationException("Database is not open. Call OpenAsync() first.");
    }

    public override void _ExitTree()
    {
        // Cleanup on node removal
        if (_isOpen)
        {
            _ = CloseAsync();
        }
        base._ExitTree();
    }
}
