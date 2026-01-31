using System;
using System.Threading.Tasks;
using Godot;
using Game.Core.Ports;
using Game.Core.Services;

namespace Game.Godot.Adapters;

public partial class DataStoreAdapter : Node, IDataStore
{
    private const string DataStoreBackendEnv = "GD_DATASTORE_BACKEND";
    private const string DataStoreDbPathEnv = "GD_DATASTORE_DB_PATH";
    private const string DefaultDbPath = "user://data/game.db";

    private SecurityFileAdapter? _securityFileAdapter;
    private SqliteDataStore? _sqlDb;
    private bool _kvReady;

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

    private SecurityFileAdapter? GetSecurityFileAdapter()
    {
        if (_securityFileAdapter != null) return _securityFileAdapter;

        // Autoload dependency: EventBus is initialized before DataStore in project.godot.
        var bus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        if (bus == null)
        {
            GD.PushWarning("[DataStoreAdapter] EventBus not found at /root/EventBus; file operations will be blocked.");
            return null;
        }

        _securityFileAdapter = new SecurityFileAdapter(bus);
        return _securityFileAdapter;
    }

    private static string MakeSafe(string key)
    {
        foreach (var invalidChar in System.IO.Path.GetInvalidFileNameChars())
            key = key.Replace(invalidChar, '_');
        return key;
    }

    private static string GetSavePath() => "user://saves";
    private static string PathFor(string key) => $"{GetSavePath()}/{MakeSafe(key)}.json";

    private static bool IsSecureMode() =>
        (GetEnv("GD_SECURE_MODE") ?? string.Empty).Trim() == "1";

    private static string GetConfiguredDbPath()
    {
        var raw = GetEnv(DataStoreDbPathEnv);
        return string.IsNullOrWhiteSpace(raw) ? DefaultDbPath : raw.Trim();
    }

    private string GetBackend()
    {
        var raw = (GetEnv(DataStoreBackendEnv) ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(raw) ? "auto" : raw;
    }

    private SqliteDataStore? GetSqlDb()
    {
        if (_sqlDb != null) return _sqlDb;
        _sqlDb = GetNodeOrNull<SqliteDataStore>("/root/SqlDb");
        return _sqlDb;
    }

    private void EnsureKvStoreReadyOrThrow()
    {
        if (_kvReady) return;

        var db = GetSqlDb();
        if (db == null)
            throw new InvalidOperationException("SqlDb not found at /root/SqlDb");

        // Ensure DB is open (avoid re-opening if already open).
        if (!db.IsOpen())
        {
            var path = GetConfiguredDbPath();
            if (!db.TryOpen(path))
                throw new InvalidOperationException(db.LastError ?? "Database open failed.");
        }

        db.Execute(SqlStatement.NoParameters(
            "CREATE TABLE IF NOT EXISTS kv_store (k TEXT PRIMARY KEY, v TEXT NOT NULL, updated_at INTEGER NOT NULL);"
        ));
        _kvReady = true;
    }

    private bool ShouldUseSqliteBackend()
    {
        var backend = GetBackend();
        if (backend == "sqlite") return true;
        if (backend == "file") return false;

        // auto: prefer sqlite when SqlDb exists
        return GetSqlDb() != null;
    }

    public Task SaveAsync(string key, string json)
    {
        if (ShouldUseSqliteBackend())
        {
            EnsureKvStoreReadyOrThrow();
            var db = GetSqlDb()!;
            var now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            db.Execute(SqlStatement.Positional(
                "INSERT INTO kv_store(k,v,updated_at) VALUES(@0,@1,@2) " +
                "ON CONFLICT(k) DO UPDATE SET v=@1, updated_at=@2;",
                key,
                json,
                now));
            return Task.CompletedTask;
        }

        return SaveFileAsync(key, json);
    }

    public Task<string?> LoadAsync(string key)
    {
        if (ShouldUseSqliteBackend())
        {
            EnsureKvStoreReadyOrThrow();
            var db = GetSqlDb()!;
            var rows = db.Query(SqlStatement.Positional("SELECT v FROM kv_store WHERE k=@0 LIMIT 1;", key));
            if (rows.Count == 0) return Task.FromResult<string?>(null);
            return Task.FromResult<string?>(rows[0].TryGetValue("v", out var value) ? value?.ToString() : null);
        }

        return LoadFileAsync(key);
    }

    public Task DeleteAsync(string key)
    {
        if (ShouldUseSqliteBackend())
        {
            EnsureKvStoreReadyOrThrow();
            var db = GetSqlDb()!;
            db.Execute(SqlStatement.Positional("DELETE FROM kv_store WHERE k=@0;", key));
            return Task.CompletedTask;
        }

        return DeleteFileAsync(key);
    }

    // Synchronous helpers for GDScript tests
    public void SaveSync(string key, string json) => SaveAsync(key, json).GetAwaiter().GetResult();
    public string? LoadSync(string key) => LoadAsync(key).GetAwaiter().GetResult();
    public void DeleteSync(string key) => DeleteAsync(key).GetAwaiter().GetResult();

    public bool TrySaveSync(string key, string json)
    {
        try
        {
            SaveSync(key, json);
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DataStoreAdapter] Save failed: {ex.GetType().Name}");
            return false;
        }
    }

    public string? TryLoadSync(string key)
    {
        try
        {
            return LoadSync(key);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DataStoreAdapter] Load failed: {ex.GetType().Name}");
            return null;
        }
    }

    private Task SaveFileAsync(string key, string json)
    {
        var sec = GetSecurityFileAdapter();
        if (sec == null)
        {
            throw new InvalidOperationException("SecurityFileAdapter not initialized");
        }

        // Validate a file path under the save directory to keep allowlist checks consistent.
        // Then create the directory (best-effort but not silent) to avoid implicit failures.
        var probePath = PathFor("__dir_probe__");
        var validatedProbePath = sec.ValidateWritePath(probePath);
        if (validatedProbePath == null)
        {
            var msg = IsSecureMode() ? "Save path not allowed." : $"Save path not allowed: {probePath}";
            throw new InvalidOperationException(msg);
        }

        var saveDirAbs = ProjectSettings.GlobalizePath(GetSavePath());
        var mkErr = DirAccess.MakeDirRecursiveAbsolute(saveDirAbs);
        if (mkErr != Error.Ok)
            throw new InvalidOperationException("Save directory creation failed.");

        var path = PathFor(key);
        var validatedPath = sec.ValidateWritePath(path);
        if (validatedPath == null)
        {
            var msg = IsSecureMode() ? "Save path not allowed." : $"Save path not allowed: {path}";
            throw new InvalidOperationException(msg);
        }

        using var file = FileAccess.Open(validatedPath.Value, FileAccess.ModeFlags.Write);
        if (file == null)
            throw new InvalidOperationException("Save write failed.");

        file.StoreString(json);
        file.Flush();
        return Task.CompletedTask;
    }

    private Task<string?> LoadFileAsync(string key)
    {
        var path = PathFor(key);
        var sec = GetSecurityFileAdapter();
        if (sec == null)
        {
            throw new InvalidOperationException("SecurityFileAdapter not initialized");
        }

        var validatedPath = sec.ValidateReadPath(path);
        if (validatedPath == null)
        {
            var msg = IsSecureMode() ? "Load path not allowed." : $"Load path not allowed: {path}";
            throw new InvalidOperationException(msg);
        }

        if (!FileAccess.FileExists(validatedPath.Value))
            return Task.FromResult<string?>(null);

        using var file = FileAccess.Open(validatedPath.Value, FileAccess.ModeFlags.Read);
        if (file == null) throw new InvalidOperationException("Load read failed.");
        return Task.FromResult<string?>(file.GetAsText());
    }

    private Task DeleteFileAsync(string key)
    {
        var path = PathFor(key);
        var sec = GetSecurityFileAdapter();
        if (sec == null)
        {
            throw new InvalidOperationException("SecurityFileAdapter not initialized");
        }

        var validatedPath = sec.ValidateWritePath(path);
        if (validatedPath == null)
        {
            var msg = IsSecureMode() ? "Delete path not allowed." : $"Delete path not allowed: {path}";
            throw new InvalidOperationException(msg);
        }

        if (FileAccess.FileExists(validatedPath.Value))
        {
            DirAccess.RemoveAbsolute(validatedPath.Value);
        }
        return Task.CompletedTask;
    }
}
