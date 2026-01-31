using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Game.Core.Ports;
using Game.Core.Services;
using Godot;
using Microsoft.Data.Sqlite;
using System.Security.Principal;

namespace Game.Godot.Adapters;

// Windows-only: Hybrid SQLite adapter.
// - If 'godot-sqlite' plugin is available, use it via dynamic calls.
// - Otherwise, fallback to Microsoft.Data.Sqlite managed provider.
public partial class SqliteDataStore : Node, ISqlDatabase
{
    private const string AuditWrittenDataKey = "SqliteDataStore.AuditWritten";
    private const string AllowPluginBackendEnv = "GD_DB_ALLOW_PLUGIN_BACKEND";
    private const string MaxDbBytesEnv = "GD_DB_MAX_BYTES";
    private const long DefaultMaxDbBytes = 100L * 1024L * 1024L; // 100 MB
    private enum Backend { Plugin, Managed }

    private SecurityFileAdapter? _securityFileAdapter;
    private Backend _backend = Backend.Managed;
    private GodotObject? _pluginDb;
    private SqliteConnection? _conn;
    private SqliteTransaction? _tx;
    private string? _dbPath;
    private string? _dbPathVirtual;
    public string? LastError { get; private set; }

    private static string? GetEnv(string key)
    {
        var envValue = OS.GetEnvironment(key);
        if (!string.IsNullOrWhiteSpace(envValue))
            return envValue;
        return System.Environment.GetEnvironmentVariable(key);
    }

    public override void _Ready()
    {
        _ = GetSecurityFileAdapter();
    }

    public bool IsOpen()
    {
        if (_backend == Backend.Plugin)
            return _pluginDb != null;
        return _conn != null;
    }

    private SecurityFileAdapter? GetSecurityFileAdapter()
    {
        if (_securityFileAdapter != null) return _securityFileAdapter;

        // Autoload dependency: EventBus is initialized before SqlDb in project.godot.
        var bus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        if (bus == null)
        {
            GD.PushWarning("[SqliteDataStore] EventBus not found at /root/EventBus; DB operations will be blocked.");
            return null;
        }

        _securityFileAdapter = new SecurityFileAdapter(bus);
        return _securityFileAdapter;
    }

    public void Open(string dbPath)
    {
        // Validate database path using SecurityFileAdapter
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("Empty database path");

        var sec = GetSecurityFileAdapter();
        if (sec == null)
        {
            GD.PrintErr("[SqliteDataStore] SecurityFileAdapter not initialized");
            throw new NotSupportedException("SecurityFileAdapter not initialized");
        }

        var validatedPath = sec.ValidateWritePath(dbPath);
        if (validatedPath == null)
        {
            GD.PrintErr($"[SqliteDataStore] Database path validation failed: {dbPath}");
            // Special-case: invalid extension under user:// should be audited distinctly.
            // SafeResourcePath rejects non-whitelisted extensions early, so we infer from the raw string here.
            var audited = false;
            try
            {
                var trimmed = dbPath.Trim();
                if (trimmed.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
                {
                    var ext = System.IO.Path.GetExtension(trimmed);
                    if (!string.IsNullOrWhiteSpace(ext) &&
                        !ext.Equals(".db", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".sqlite", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".sqlite3", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!IncludeSensitiveDetails())
                        {
                            Audit("db.sqlite.invalid_extension", ext, trimmed, caller: nameof(SqliteDataStore));
                            audited = true;
                        }
                    }
                }
            }
            catch { }

            var ex = new NotSupportedException($"Database path not allowed (expected user://): {dbPath}");
            if (audited) ex.Data[AuditWrittenDataKey] = true;
            throw ex;
        }

        var extension = System.IO.Path.GetExtension(validatedPath.Value);
        var isAllowedDbExtension =
            extension.Equals(".db", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".sqlite", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".sqlite3", StringComparison.OrdinalIgnoreCase);
        if (!isAllowedDbExtension)
        {
            var reason = string.IsNullOrWhiteSpace(extension) ? "(none)" : extension;
            var audited = false;
            if (!IncludeSensitiveDetails())
            {
                Audit("db.sqlite.invalid_extension", reason, validatedPath.Value, caller: nameof(SqliteDataStore));
                audited = true;
            }

            var ex = new NotSupportedException($"Database extension not allowed: {reason}");
            if (audited) ex.Data[AuditWrittenDataKey] = true;
            throw ex;
        }

        _dbPathVirtual = validatedPath.Value;
        _dbPath = Globalize(_dbPathVirtual);
        EnsureParentDir(_dbPath!);
        var exists = System.IO.File.Exists(_dbPath!);
        if (exists)
        {
            var maxBytes = GetMaxDbBytes();
            var sizeBytes = new System.IO.FileInfo(_dbPath!).Length;
            if (sizeBytes > maxBytes)
            {
                var reason = $"size_bytes={sizeBytes} max_bytes={maxBytes}";
                var audited = false;
                if (!IncludeSensitiveDetails())
                {
                    Audit("db.sqlite.size_limit_exceeded", reason, _dbPathVirtual, caller: nameof(SqliteDataStore));
                    audited = true;
                }

                var ex = new System.Security.SecurityException($"Database file too large: {reason}");
                if (audited) ex.Data[AuditWrittenDataKey] = true;
                throw ex;
            }
        }

        var isNew = !exists;

        var prefer = (System.Environment.GetEnvironmentVariable("GODOT_DB_BACKEND") ?? string.Empty).Trim().ToLowerInvariant();
        var wantsPlugin = prefer == "plugin";
        var wantsManaged = string.IsNullOrWhiteSpace(prefer) || prefer == "managed";

        if (!wantsManaged && !wantsPlugin)
            wantsManaged = true;

        var allowPlugin = wantsPlugin && IsPluginBackendAllowed();
        if (wantsPlugin && !allowPlugin)
        {
            var allowFlag = (System.Environment.GetEnvironmentVariable(AllowPluginBackendEnv) ?? string.Empty).Trim();
            var isSecureMode = System.Environment.GetEnvironmentVariable("GD_SECURE_MODE") == "1";
            var isCi = !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("CI"));
            var reason = $"blocked_by_policy {AllowPluginBackendEnv}={allowFlag} gd_secure_mode={(isSecureMode ? "1" : "0")} ci={(isCi ? "1" : "0")}";

            var audited = false;
            if (!IncludeSensitiveDetails())
            {
                Audit("db.sqlite.plugin_backend_denied", reason, _dbPathVirtual, caller: nameof(SqliteDataStore));
                audited = true;
            }

            var ex = new NotSupportedException("Plugin backend is restricted to development/experimental environments.");
            if (audited) ex.Data[AuditWrittenDataKey] = true;
            throw ex;
        }

        if (allowPlugin && TryOpenPlugin(_dbPath!))
        {
            _backend = Backend.Plugin;
            if (isNew) TryInitSchema();
            EnsureSchemaVersionMetaBestEffort();
            return;
        }

        if (wantsPlugin)
            throw new NotSupportedException("godot-sqlite plugin requested via GODOT_DB_BACKEND=plugin but not available.");

        // Managed path
        var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath!, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
        _conn = new SqliteConnection(cs);
        _conn.Open();
        _backend = Backend.Managed;
        TryHardenDbAclBestEffort();
        // Enable FK + configurable journal mode (default WAL; override via GD_DB_JOURNAL=DELETE|WAL|TRUNCATE|MEMORY|PERSIST)
        using var cmd = _conn.CreateCommand();
        var journal = (GetEnv("GD_DB_JOURNAL") ?? "WAL").ToUpperInvariant();
        switch (journal)
        {
            case "WAL": case "DELETE": case "TRUNCATE": case "MEMORY": case "PERSIST": break;
            default: journal = "WAL"; break;
        }
        cmd.CommandText = $"PRAGMA foreign_keys=ON; PRAGMA journal_mode={journal}; PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();
        GD.Print("[DB] backend=managed (Microsoft.Data.Sqlite)");
        if (isNew) TryInitSchema();
        EnsureSchemaVersionMetaBestEffort();
    }

    private void EnsureSchemaVersionMetaBestEffort()
    {
        try
        {
            if (!TableExists("schema_version"))
                return;

            var cols = Query(SqlStatement.NoParameters("PRAGMA table_info(schema_version);"));
            var hasId = cols.Any(r =>
            {
                if (!r.TryGetValue("name", out var nameValue) || nameValue is null) return false;
                return string.Equals(nameValue.ToString(), "id", StringComparison.OrdinalIgnoreCase);
            });
            if (hasId)
                return;

            // Legacy schema_version: version INTEGER PRIMARY KEY (and optional extra columns).
            var rows = Query(SqlStatement.NoParameters("SELECT MAX(version) AS v FROM schema_version;"));
            var vObj = rows.Count > 0 && rows[0].TryGetValue("v", out var maxVersionValue) ? maxVersionValue : null;
            var maxVersion = 1;
            if (vObj != null)
            {
                try { maxVersion = Convert.ToInt32(vObj); } catch { maxVersion = 1; }
            }
            if (maxVersion < 1) maxVersion = 1;

            Execute(SqlStatement.NoParameters("ALTER TABLE schema_version RENAME TO schema_version_legacy;"));
            Execute(SqlStatement.NoParameters("CREATE TABLE schema_version (id INTEGER PRIMARY KEY CHECK (id = 1), version INTEGER NOT NULL);"));
            Execute(SqlStatement.Positional("INSERT INTO schema_version(id, version) VALUES(1, @0);", maxVersion));
            Execute(SqlStatement.NoParameters("DROP TABLE schema_version_legacy;"));
        }
        catch
        {
            // Best-effort: schema meta normalization should never block DB open.
        }
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
        catch (Exception ex)
        {
            var includeSensitiveDetails = IncludeSensitiveDetails();
            LastError = includeSensitiveDetails ? ex.Message : "Database open failed.";
            if (!includeSensitiveDetails)
            {
                var alreadyAudited = ex.Data.Contains(AuditWrittenDataKey) && ex.Data[AuditWrittenDataKey] as bool? == true;
                if (!alreadyAudited)
                    Audit("db.sqlite.open_failed", BuildAuditReason(ex), dbPath, caller: nameof(SqliteDataStore));
            }
            return false;
        }
    }

    public bool TableExists(string name)
    {
        var rows = Query(SqlStatement.Positional(
            "SELECT name FROM sqlite_master WHERE type=@0 AND name=@1;",
            "table",
            name));
        return rows.Count > 0;
    }

    public int Execute(SqlStatement stmt)
    {
        var (sql, parameters) = ToPositional(stmt);
        var includeSensitiveDetails = IncludeSensitiveDetails();
        if (_backend == Backend.Plugin)
        {
            try
            {
                var formattedSql = FormatSqlWithParameters(sql, parameters);
                var ok = _pluginDb!.Call("Query", formattedSql).AsBool();
                if (!ok)
                    throw new InvalidOperationException("SQL execution failed (plugin)");

                return 0;
            }
            catch (Exception ex)
            {
                if (!includeSensitiveDetails)
                {
                    Audit("db.sqlite.nonquery_failed", BuildAuditReason(ex), _dbPathVirtual ?? "unknown", caller: nameof(SqliteDataStore));
                }

                throw DatabaseErrorHandling.CreateOperationException(
                    operation: "nonquery",
                    dbPath: _dbPathVirtual ?? "unknown",
                    sql: includeSensitiveDetails ? Truncate(sql, 120) : null,
                    ex: ex,
                    includeSensitiveDetails: includeSensitiveDetails);
            }
        }
        else
        {
            try
            {
                using var cmd = BuildCommand(sql, parameters);
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                if (!includeSensitiveDetails)
                {
                    Audit("db.sqlite.nonquery_failed", BuildAuditReason(ex), _dbPathVirtual ?? "unknown", caller: nameof(SqliteDataStore));
                }

                throw DatabaseErrorHandling.CreateOperationException(
                    operation: "nonquery",
                    dbPath: _dbPathVirtual ?? "unknown",
                    sql: includeSensitiveDetails ? Truncate(sql, 120) : null,
                    ex: ex,
                    includeSensitiveDetails: includeSensitiveDetails);
            }
        }
    }

    public System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object?>> Query(SqlStatement stmt)
    {
        var (sql, parameters) = ToPositional(stmt);
        var includeSensitiveDetails = IncludeSensitiveDetails();
        if (_backend == Backend.Plugin)
        {
            try
            {
                var formattedSql = FormatSqlWithParameters(sql, parameters);
                var ok = _pluginDb!.Call("Query", formattedSql).AsBool();
                if (!ok)
                    throw new InvalidOperationException("SQL query failed (plugin)");

                var resultObj = _pluginDb.Get("QueryResult");
                var arr = resultObj.As<global::Godot.Collections.Array>();
                var list = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object?>>();
                foreach (var item in arr)
                {
                    var row = item.As<global::Godot.Collections.Dictionary>();
                    var dict = new System.Collections.Generic.Dictionary<string, object?>();
                    foreach (var key in row.Keys)
                    {
                        var keyName = key.AsStringName();
                        dict[keyName] = row[key];
                    }
                    list.Add(dict);
                }
                return list;
            }
            catch (Exception ex)
            {
                if (!includeSensitiveDetails)
                {
                    Audit("db.sqlite.query_failed", BuildAuditReason(ex), _dbPathVirtual ?? "unknown", caller: nameof(SqliteDataStore));
                }

                throw DatabaseErrorHandling.CreateOperationException(
                    operation: "query",
                    dbPath: _dbPathVirtual ?? "unknown",
                    sql: includeSensitiveDetails ? Truncate(sql, 120) : null,
                    ex: ex,
                    includeSensitiveDetails: includeSensitiveDetails);
            }
        }
        else
        {
            try
            {
                using var cmd = BuildCommand(sql, parameters);
                using var reader = cmd.ExecuteReader();
                var list = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object?>>();
                while (reader.Read())
                {
                    var row = new System.Collections.Generic.Dictionary<string, object?>();
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
            catch (Exception ex)
            {
                if (!includeSensitiveDetails)
                {
                    Audit("db.sqlite.query_failed", BuildAuditReason(ex), _dbPathVirtual ?? "unknown", caller: nameof(SqliteDataStore));
                }

                throw DatabaseErrorHandling.CreateOperationException(
                    operation: "query",
                    dbPath: _dbPathVirtual ?? "unknown",
                    sql: includeSensitiveDetails ? Truncate(sql, 120) : null,
                    ex: ex,
                    includeSensitiveDetails: includeSensitiveDetails);
            }
        }
    }

    private static (string Sql, object[] Parameters) ToPositional(SqlStatement stmt)
    {
        if (stmt.Parameters.Count == 0)
            return (stmt.Text, Array.Empty<object>());

        var indexed = new List<(int Index, object? Value)>();
        foreach (var kv in stmt.Parameters)
        {
            var key = kv.Key;
            if (!key.StartsWith("@", StringComparison.Ordinal))
                throw new ArgumentException($"Invalid parameter name: {key}", nameof(stmt));

            if (!int.TryParse(key.AsSpan(1), out var idx) || idx < 0)
                throw new ArgumentException($"Expected positional parameter like '@0', got: {key}", nameof(stmt));

            indexed.Add((idx, kv.Value));
        }

        indexed.Sort((a, b) => a.Index.CompareTo(b.Index));
        for (var i = 0; i < indexed.Count; i++)
        {
            if (indexed[i].Index != i)
                throw new ArgumentException("Positional parameters must be contiguous: @0..@N", nameof(stmt));
        }

        return (stmt.Text, indexed.Select(x => (object)(x.Value ?? DBNull.Value)).ToArray());
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
            var value = ProjectSettings.GetSetting("application/config/name");
            return value.VariantType == Variant.Type.Nil ? "GodotGame" : value.AsString();
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
            var instance = ClassDB.Instantiate("SQLite");
            if (instance.VariantType != Variant.Type.Object) return false;
            var obj = instance.As<GodotObject>();
            _pluginDb = obj;
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

            // Validate schema path using SecurityFileAdapter
            var sec = GetSecurityFileAdapter();
            if (sec == null)
            {
                GD.PrintErr("[SqliteDataStore] SecurityFileAdapter not initialized");
                return;
            }

            var validatedSchemaPath = sec.ValidateReadPath(schemaPath);
            if (validatedSchemaPath == null)
            {
                GD.PrintErr($"[SqliteDataStore] Schema file access denied: {schemaPath}");
                return;
            }

            using var file = FileAccess.Open(validatedSchemaPath.Value, FileAccess.ModeFlags.Read);
            if (file == null) return;
            var script = file.GetAsText();
            foreach (var stmt in SplitSql(script))
            {
                var statementSql = StripSqlComments(stmt).Trim();
                if (string.IsNullOrWhiteSpace(statementSql)) continue;
                Execute(SqlStatement.NoParameters(statementSql));
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"Schema init failed: {ex.Message}");
        }
    }

    private static string StripSqlComments(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return string.Empty;
        var lines = sql.Split('\n');
        var sb = new StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.Replace("\r", string.Empty);
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("--", StringComparison.Ordinal)) continue;
            var idx = line.IndexOf("--", StringComparison.Ordinal);
            if (idx >= 0) line = line.Substring(0, idx);
            if (string.IsNullOrWhiteSpace(line)) continue;
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    private static IEnumerable<string> SplitSql(string sql)
    {
        // naive split by ';' keeping simple cases; ignores ';' inside strings (acceptable for our schema)
        var parts = sql.Split(';');
        foreach (var part in parts) yield return part;
    }

    private static string FormatSqlWithParameters(string sql, object[] parameters)
    {
        if (parameters == null || parameters.Length == 0) return sql;
        // Replace in reverse order to avoid replacing "@1" inside "@10".
        for (int i = parameters.Length - 1; i >= 0; i--)
        {
            var parameterValue = parameters[i];
            if (parameterValue is DBNull) parameterValue = null;
            var literal = parameterValue switch
            {
                null => "NULL",
                string str => $"'{str.Replace("'", "''")}'",
                bool boolValue => boolValue ? "1" : "0",
                float floatValue => floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                double doubleValue => doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                IFormattable fmt => fmt.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => $"'{parameterValue.ToString()?.Replace("'", "''")}'"
            };
            sql = sql.Replace($"@{i}", literal);
        }
        // Ensure no unresolved positional placeholders remain.
        for (var i = 0; i < sql.Length; i++)
        {
            if (sql[i] != '@') continue;
            if (i + 1 >= sql.Length || !char.IsDigit(sql[i + 1])) continue;
            throw new ArgumentException("Unresolved positional parameter placeholders remain after formatting.", nameof(sql));
        }
        return sql;
    }

    private SqliteCommand BuildCommand(string sql, object[] parameters)
    {
        var cmd = _conn!.CreateCommand();
        cmd.CommandText = sql;
        ApplyCommandTimeout(cmd);
        if (_tx != null) cmd.Transaction = _tx;
        if (parameters != null)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = cmd.CreateParameter();
                parameter.ParameterName = $"@{i}";
                parameter.Value = parameters[i] ?? DBNull.Value;
                cmd.Parameters.Add(parameter);
            }
        }
        return cmd;
    }

    private static void ApplyCommandTimeout(SqliteCommand cmd)
    {
        var timeoutEnv = System.Environment.GetEnvironmentVariable("GD_DB_COMMAND_TIMEOUT_SEC");
        if (!string.IsNullOrWhiteSpace(timeoutEnv) && int.TryParse(timeoutEnv, out var timeoutSeconds) && timeoutSeconds >= 0)
        {
            cmd.CommandTimeout = timeoutSeconds;
            return;
        }

        // Default: enforce a finite timeout only when sensitive details are disabled (CI/secure).
        // In local debug we keep dev ergonomics (infinite by default) unless overridden via env var.
        if (!IncludeSensitiveDetails())
            cmd.CommandTimeout = 30;
    }

    private void TryHardenDbAclBestEffort()
    {
        var harden = System.Environment.GetEnvironmentVariable("GD_DB_HARDEN_ACL") == "1";
        if (!harden) return;
        if (!OperatingSystem.IsWindows()) return;
        if (IncludeSensitiveDetails()) return;
        if (string.IsNullOrWhiteSpace(_dbPath)) return;

        var files = new List<string> { _dbPath! };
        files.Add(_dbPath! + "-wal");
        files.Add(_dbPath! + "-shm");

        var hardened = 0;
        foreach (var abs in files)
        {
            try
            {
                if (!System.IO.File.Exists(abs))
                    continue;

                if (!TryHardenWithIcacls(abs, out var failureReason))
                {
                    Audit("db.sqlite.acl_harden_failed", failureReason, _dbPathVirtual ?? "unknown", caller: nameof(SqliteDataStore));
                    continue;
                }

                hardened++;
            }
            catch (Exception ex)
            {
                Audit("db.sqlite.acl_harden_failed", BuildAuditReason(ex), _dbPathVirtual ?? "unknown", caller: nameof(SqliteDataStore));
            }
        }

        if (hardened > 0)
            Audit("db.sqlite.acl_harden_ok", "success", _dbPathVirtual ?? "unknown", caller: nameof(SqliteDataStore));
    }

    private static bool TryHardenWithIcacls(string absoluteFilePath, out string failureReason)
    {
        failureReason = "unknown";

        try
        {
            if (!System.IO.Path.IsPathFullyQualified(absoluteFilePath))
            {
                failureReason = "not_absolute_path";
                return false;
            }

            var userSid = WindowsIdentity.GetCurrent().User?.Value;
            if (string.IsNullOrWhiteSpace(userSid))
            {
                failureReason = "no_current_user_sid";
                return false;
            }

            var icaclsExe = System.IO.Path.Combine(System.Environment.SystemDirectory, "icacls.exe");
            if (!System.IO.File.Exists(icaclsExe))
            {
                failureReason = "icacls_not_found";
                return false;
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = icaclsExe,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            psi.ArgumentList.Add(absoluteFilePath);
            psi.ArgumentList.Add("/inheritance:r");
            psi.ArgumentList.Add("/grant:r");
            psi.ArgumentList.Add($"*{userSid}:(F)");
            psi.ArgumentList.Add("/grant:r");
            psi.ArgumentList.Add("*S-1-5-18:(F)"); // LocalSystem
            psi.ArgumentList.Add("/grant:r");
            psi.ArgumentList.Add("*S-1-5-32-544:(F)"); // Builtin Administrators

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null)
            {
                failureReason = "process_start_failed";
                return false;
            }

            if (!proc.WaitForExit(5000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                failureReason = "timeout";
                return false;
            }

            if (proc.ExitCode != 0)
            {
                failureReason = $"icacls_exit_code={proc.ExitCode}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            failureReason = ex.GetType().Name;
            return false;
        }
    }

    private static bool IncludeSensitiveDetails()
    {
#if DEBUG
        var isDebugBuild = true;
#else
        var isDebugBuild = false;
#endif

        return SensitiveDetailsPolicy.IncludeSensitiveDetails(isDebugBuild, GetEnv);
    }

    private static long GetMaxDbBytes()
    {
        var raw = GetEnv(MaxDbBytesEnv);
        if (string.IsNullOrWhiteSpace(raw))
            return DefaultMaxDbBytes;

        if (long.TryParse(raw, out var parsed) && parsed > 0)
            return parsed;

        return DefaultMaxDbBytes;
    }

    private static bool IsPluginBackendAllowed()
    {
        var allow = (GetEnv(AllowPluginBackendEnv) ?? string.Empty).Trim() == "1";
        if (!allow)
            return false;

        // Restrict plugin backend to development/experimental environment only.
        // - Requires DEBUG build
        // - Blocked in GD_SECURE_MODE=1 and in CI
        return IncludeSensitiveDetails();
    }

    private static string BuildAuditReason(Exception ex)
    {
        if (ex is SqliteException sqliteEx)
        {
            return $"SqliteException code={sqliteEx.SqliteErrorCode}";
        }

        return ex.GetType().Name;
    }

    private static void Audit(string action, string reason, string target, string caller)
    {
        try
        {
            var date = System.DateTime.UtcNow.ToString("yyyy-MM-dd");
            var root = GetEnv("AUDIT_LOG_ROOT");
            string path;
            if (!string.IsNullOrWhiteSpace(root))
            {
                System.IO.Directory.CreateDirectory(root);
                path = System.IO.Path.Combine(root, "security-audit.jsonl");
            }
            else
            {
                var isCi = !string.IsNullOrWhiteSpace(GetEnv("CI"));
                var isSecureOrTest = string.Equals(GetEnv("GD_SECURE_MODE"), "1", StringComparison.Ordinal) ||
                                     string.Equals(GetEnv("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal);
                if (isCi || isSecureOrTest)
                {
                    var dir = ProjectSettings.GlobalizePath($"user://logs/ci/{date}");
                    System.IO.Directory.CreateDirectory(dir);
                    path = System.IO.Path.Combine(dir, "security-audit.jsonl");
                }
                else
                {
                    var dir = ProjectSettings.GlobalizePath("user://logs/security");
                    System.IO.Directory.CreateDirectory(dir);
                    path = System.IO.Path.Combine(dir, "security-audit.jsonl");
                }
            }

            var obj = new System.Collections.Generic.Dictionary<string, object?>
            {
                ["ts"] = System.DateTime.UtcNow.ToString("o"),
                ["action"] = action,
                ["reason"] = Truncate(reason ?? string.Empty, 500),
                ["target"] = Truncate(target ?? string.Empty, 1000),
                ["caller"] = caller
            };
            var json = System.Text.Json.JsonSerializer.Serialize(obj);
            System.IO.File.AppendAllText(path, json + System.Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch { }
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
        return s.Length <= max ? s : s.Substring(0, max);
    }
}
