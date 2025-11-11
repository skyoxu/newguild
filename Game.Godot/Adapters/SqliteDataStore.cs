using System;
using System.Collections.Generic;
using System.Data;
using Game.Core.Ports;
using Godot;
using Godot.Collections;
using Microsoft.Data.Sqlite;

namespace Game.Godot.Adapters;

// Windows-only: Hybrid SQLite adapter.
// - If 'godot-sqlite' plugin is available, use it via dynamic calls.
// - Otherwise, fallback to Microsoft.Data.Sqlite managed provider.
public partial class SqliteDataStore : Node, ISqlDatabase
{
    private enum Backend { Plugin, Managed }

    private Backend _backend = Backend.Managed;
    private GodotObject? _pluginDb;
    private SqliteConnection? _conn;
    private SqliteTransaction? _tx;
    private string? _dbPath;
    public string? LastError { get; private set; }

    public void Open(string dbPath)
    {
        _dbPath = Globalize(dbPath);
        EnsureParentDir(_dbPath!);
        var isNew = !System.IO.File.Exists(_dbPath!);
        var prefer = (System.Environment.GetEnvironmentVariable("GODOT_DB_BACKEND") ?? string.Empty).ToLowerInvariant(); var forcePlugin = prefer == "plugin"; var forceManaged = prefer == "managed"; if (!forceManaged && TryOpenPlugin(_dbPath!))
        {
            _backend = Backend.Plugin;
            if (isNew) TryInitSchema();
            return;
        }

        if (forcePlugin) throw new NotSupportedException("godot-sqlite plugin requested via GODOT_DB_BACKEND=plugin but not available.");

        // Managed path
        var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath!, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
        _conn = new SqliteConnection(cs);
        _conn.Open();
        _backend = Backend.Managed;
        // Enable FK + WAL
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();
        GD.Print("[DB] backend=managed (Microsoft.Data.Sqlite)");
        if (isNew) TryInitSchema();
    }

    public void Close()
    {
        if (_backend == Backend.Plugin)
        {
            // Best-effort: call CloseDb if available
            try { _pluginDb?.Call("CloseDb"); } catch { }
            _pluginDb = null;
        }
        else
        {
            _tx?.Dispose();
            _conn?.Dispose();
            _tx = null;
            _conn = null;
        }
    }

    // GDScript-friendly helpers
    public bool TryOpen(string dbPath)
    {
        try { Open(dbPath); LastError = null; return true; }
        catch (Exception ex) { LastError = ex.Message; return false; }
    }

    public bool TableExists(string name)
    {
        var rows = Query("SELECT name FROM sqlite_master WHERE type='table' AND name=@0;", name);
        return rows.Count > 0;
    }

    public int Execute(string sql, params object[] parameters)
    {
        if (_backend == Backend.Plugin)
        {
            var s = FormatSqlWithParameters(sql, parameters);
            var ok = _pluginDb!.Call("Query", s).AsBool();
            if (!ok) throw new InvalidOperationException("SQL execution failed (plugin)");
            return 0;
        }
        else
        {
            using var cmd = BuildCommand(sql, parameters);
            return cmd.ExecuteNonQuery();
        }
    }

    public List<Dictionary<string, object?>> Query(string sql, params object[] parameters)
    {
        if (_backend == Backend.Plugin)
        {
            var s = FormatSqlWithParameters(sql, parameters);
            var ok = _pluginDb!.Call("Query", s).AsBool();
            if (!ok) throw new InvalidOperationException("SQL query failed (plugin)");
            var resultObj = _pluginDb.Get("QueryResult");
            var arr = resultObj.As<Array>();
            var list = new List<Dictionary<string, object?>>();
            foreach (var item in arr)
            {
                var row = item.As<Dictionary>();
                var dict = new Dictionary<string, object?>();
                foreach (var key in row.Keys)
                {
                    var k = key.AsStringName();
                    dict[k] = row[key];
                }
                list.Add(dict);
            }
            return list;
        }
        else
        {
            using var cmd = BuildCommand(sql, parameters);
            using var reader = cmd.ExecuteReader();
            var list = new List<Dictionary<string, object?>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var name = reader.GetName(i);
                    var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[name] = val;
                }
                list.Add(row);
            }
            return list;
        }
    }

    public void BeginTransaction()
    {
        if (_backend == Backend.Plugin)
        {
            var ok = _pluginDb!.Call("Query", "BEGIN TRANSACTION;").AsBool();
            if (!ok) throw new InvalidOperationException("BEGIN failed (plugin)");
        }
        else
        {
            if (_tx != null) throw new InvalidOperationException("Transaction already in progress");
            _tx = _conn!.BeginTransaction();
        }
    }

    public void CommitTransaction()
    {
        if (_backend == Backend.Plugin)
        {
            var ok = _pluginDb!.Call("Query", "COMMIT;").AsBool();
            if (!ok) throw new InvalidOperationException("COMMIT failed (plugin)");
        }
        else
        {
            if (_tx == null) throw new InvalidOperationException("No transaction in progress");
            _tx.Commit();
            _tx.Dispose();
            _tx = null;
        }
    }

    public void RollbackTransaction()
    {
        if (_backend == Backend.Plugin)
        {
            var ok = _pluginDb!.Call("Query", "ROLLBACK;").AsBool();
            if (!ok) throw new InvalidOperationException("ROLLBACK failed (plugin)");
        }
        else
        {
            if (_tx == null) throw new InvalidOperationException("No transaction in progress");
            _tx.Rollback();
            _tx.Dispose();
            _tx = null;
        }
    }

    private string Globalize(string path)
    {
        // Support Godot paths (user://, res://). If engine not initialized, fallback to absolute.
        try { return ProjectSettings.GlobalizePath(path); } catch { }
        return path.Replace("user://", System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Godot", "app_userdata", GetAppName()) + System.IO.Path.DirectorySeparatorChar);
    }

    private static string GetAppName()
    {
        try
        {
            var v = ProjectSettings.GetSetting("application/config/name");
            return v.VariantType == Variant.Type.Nil ? "GodotGame" : v.AsString();
        }
        catch { return "GodotGame"; }
    }

    private static void EnsureParentDir(string absPath)
    {
        var dir = System.IO.Path.GetDirectoryName(absPath);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);
    }

    private bool TryOpenPlugin(string absPath)
    {
        try
        {
            var obj = ClassDB.Instantiate("SQLite");
            if (obj is not GodotObject go) return false;
            _pluginDb = go;
            // Prefer setting Path then calling OpenDb, if exposed
            try { _pluginDb.Set("Path", absPath); } catch { }
            var ok = _pluginDb.Call("OpenDb").AsBool();
            if (!ok)
            {
                // Some variants expect path in open
                ok = _pluginDb.Call("OpenDb", absPath).AsBool();
            }
            if (ok) GD.Print("[DB] backend=plugin (godot-sqlite)"); return ok;
        }
        catch
        {
            return false;
        }
    }

    private void TryInitSchema()
    {
        try
        {
            // Load schema script from res://
            var schemaPath = "res://scripts/db/schema.sql";
            using var f = FileAccess.Open(schemaPath, FileAccess.ModeFlags.Read);
            if (f == null) return;
            var script = f.GetAsText();
            foreach (var stmt in SplitSql(script))
            {
                var s = stmt.Trim();
                if (string.IsNullOrWhiteSpace(s)) continue;
                Execute(s);
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"Schema init failed: {ex.Message}");
        }
    }

    private static IEnumerable<string> SplitSql(string sql)
    {
        // naive split by ';' keeping simple cases; ignores ';' inside strings (acceptable for our schema)
        var parts = sql.Split(';');
        foreach (var p in parts) yield return p;
    }

    private static string FormatSqlWithParameters(string sql, object[] parameters)
    {
        if (parameters == null || parameters.Length == 0) return sql;
        for (int i = 0; i < parameters.Length; i++)
        {
            var v = parameters[i];
            var s = v switch
            {
                null => "NULL",
                string str => $"'{str.Replace("'", "''")}'",
                bool b => b ? "1" : "0",
                float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
                double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                IFormattable fmt => fmt.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => $"'{v.ToString()?.Replace("'", "''")}'"
            };
            sql = sql.Replace($"@{i}", s);
        }
        return sql;
    }

    private SqliteCommand BuildCommand(string sql, object[] parameters)
    {
        var cmd = _conn!.CreateCommand();
        cmd.CommandText = sql;
        if (_tx != null) cmd.Transaction = _tx;
        if (parameters != null)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = $"@{i}";
                p.Value = parameters[i] ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
        }
        return cmd;
    }
}




