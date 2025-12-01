using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Game.Core.Ports;
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
    private SqliteConnection? _connection;
    private bool _isOpen;
    private readonly string _dbPath;

    public GodotSQLiteDatabase(string dbPath = "user://game.db")
    {
        // Security: only allow user:// paths (ADR-0002)
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("Database path cannot be empty", nameof(dbPath));

        var normalized = dbPath.Replace('\\', '/').ToLowerInvariant();
        if (!normalized.StartsWith("user://"))
            throw new NotSupportedException("Only user:// paths are allowed for database files (ADR-0002)");

        if (normalized.Contains(".."))
            throw new NotSupportedException("Path traversal is not allowed (ADR-0002)");

        _dbPath = ProjectSettings.GlobalizePath(dbPath);
    }

    public Task OpenAsync()
    {
        if (_isOpen) return Task.CompletedTask;

        try
        {
            // Ensure parent directory exists
            var dir = System.IO.Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            _isOpen = true;

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open database at {_dbPath}", ex);
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
            throw new InvalidOperationException("Failed to close database", ex);
        }
    }

    public Task<int> ExecuteNonQueryAsync(string sql, Dictionary<string, object>? parameters = null)
    {
        EnsureOpen();

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

            int result = command.ExecuteNonQuery();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to execute non-query: {sql}", ex);
        }
    }

    public Task<object?> ExecuteScalarAsync(string sql, Dictionary<string, object>? parameters = null)
    {
        EnsureOpen();

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
            throw new InvalidOperationException($"Failed to execute scalar: {sql}", ex);
        }
    }

    public Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(string sql, Dictionary<string, object>? parameters = null)
    {
        EnsureOpen();

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
            throw new InvalidOperationException($"Failed to execute query: {sql}", ex);
        }
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
