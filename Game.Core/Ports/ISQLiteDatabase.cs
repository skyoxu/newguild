using System.Collections.Generic;
using System.Threading.Tasks;

namespace Game.Core.Ports;

/// <summary>
/// Port interface for SQLite database operations.
/// Follows ADR-0018 (pure C# interface, zero Godot dependencies).
/// Godot adapter implementation will use godot-sqlite plugin.
/// </summary>
public interface ISQLiteDatabase
{
    /// <summary>
    /// Executes a SQL query that returns no results (INSERT, UPDATE, DELETE).
    /// </summary>
    /// <param name="stmt">SQL statement with explicit parameters</param>
    /// <returns>Number of rows affected</returns>
    /// <exception cref="InvalidOperationException">The operation fails. Implementations should sanitize messages when sensitive details are disabled (ADR-0019).</exception>
    Task<int> ExecuteNonQueryAsync(SqlStatement stmt);

    /// <summary>
    /// Executes a SQL query and returns the first scalar value.
    /// </summary>
    /// <param name="stmt">SQL statement with explicit parameters</param>
    /// <returns>First column of first row, or null if no results</returns>
    /// <exception cref="InvalidOperationException">The operation fails. Implementations should sanitize messages when sensitive details are disabled (ADR-0019).</exception>
    Task<object?> ExecuteScalarAsync(SqlStatement stmt);

    /// <summary>
    /// Executes a SQL query and returns all matching rows.
    /// </summary>
    /// <param name="stmt">SQL statement with explicit parameters</param>
    /// <returns>List of rows, where each row is a dictionary of column values</returns>
    /// <exception cref="InvalidOperationException">The operation fails. Implementations should sanitize messages when sensitive details are disabled (ADR-0019).</exception>
    Task<IReadOnlyList<Dictionary<string, object>>> QueryAsync(SqlStatement stmt);

    /// <summary>
    /// Opens a database connection if not already open.
    /// </summary>
    /// <exception cref="InvalidOperationException">The connection cannot be opened. Implementations should emit an ADR-0019 compliant audit entry on failure.</exception>
    Task OpenAsync();

    /// <summary>
    /// Closes the database connection.
    /// </summary>
    /// <exception cref="InvalidOperationException">The connection cannot be closed. Implementations should emit an ADR-0019 compliant audit entry on failure.</exception>
    Task CloseAsync();
}
