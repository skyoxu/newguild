using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Game.Core.Ports;
using Microsoft.Data.Sqlite;

namespace Game.Core.Repositories;

/// <summary>
/// File-backed SQLite implementation for deterministic core tests.
/// Follows ADR-0018 (pure C# implementation, zero Godot dependencies).
/// </summary>
internal sealed class FileSQLiteDatabase : ISQLiteDatabase, IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _connection;
    private bool _isOpen;

    public FileSQLiteDatabase(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("Database path cannot be empty.", nameof(dbPath));

        _dbPath = Path.GetFullPath(dbPath);
    }

    public Task OpenAsync()
    {
        if (_isOpen)
            return Task.CompletedTask;

        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();
        _isOpen = true;
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        if (!_isOpen || _connection == null)
            return Task.CompletedTask;

        _connection.Close();
        _connection.Dispose();
        _connection = null;
        _isOpen = false;
        return Task.CompletedTask;
    }

    public Task<int> ExecuteNonQueryAsync(SqlStatement stmt)
    {
        EnsureOpen();
        using var command = CreateCommand(stmt);
        var rows = command.ExecuteNonQuery();
        return Task.FromResult(rows);
    }

    public Task<object?> ExecuteScalarAsync(SqlStatement stmt)
    {
        EnsureOpen();
        using var command = CreateCommand(stmt);
        var result = command.ExecuteScalar();
        return Task.FromResult(result == DBNull.Value ? null : result);
    }

    public Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(SqlStatement stmt)
    {
        EnsureOpen();
        using var command = CreateCommand(stmt);
        using var reader = command.ExecuteReader();
        var results = new List<Dictionary<string, object>>();

        while (reader.Read())
        {
            var row = new Dictionary<string, object>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.GetValue(i);
            }
            results.Add(row);
        }

        return Task.FromResult<IReadOnlyList<Dictionary<string, object>>>(results);
    }

    private void EnsureOpen()
    {
        if (!_isOpen || _connection == null)
            throw new InvalidOperationException("Database is not open. Call OpenAsync() first.");
    }

    private SqliteCommand CreateCommand(SqlStatement stmt)
    {
        var command = _connection!.CreateCommand();
        command.CommandText = stmt.Text;
        foreach (var param in stmt.Parameters)
        {
            command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
        }
        return command;
    }

    public void Dispose()
    {
        if (_connection == null) return;
        _connection.Dispose();
        _connection = null;
        _isOpen = false;
    }
}
