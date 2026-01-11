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
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            key = key.Replace(c, '_');
        return key;
    }

    private static string GetSavePath() => "user://saves";
    private static string PathFor(string key) => $"{GetSavePath()}/{MakeSafe(key)}.json";

    private static string GetConfiguredDbPath()
    {
        var raw = System.Environment.GetEnvironmentVariable(DataStoreDbPathEnv);
        return string.IsNullOrWhiteSpace(raw) ? DefaultDbPath : raw.Trim();
    }

    private string GetBackend()
    {
        var raw = (System.Environment.GetEnvironmentVariable(DataStoreBackendEnv) ?? string.Empty).Trim().ToLowerInvariant();
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
            return Task.FromResult<string?>(rows[0].TryGetValue("v", out var v) ? v?.ToString() : null);
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
    public void SaveSync(string key, string json) => SaveAsync(key, json).Wait();
    public string? LoadSync(string key) => LoadAsync(key).Result;
    public void DeleteSync(string key) => DeleteAsync(key).Wait();

    private Task SaveFileAsync(string key, string json)
    {
        var sec = GetSecurityFileAdapter();
        if (sec == null)
        {
            GD.PrintErr("[DataStoreAdapter] SecurityFileAdapter not initialized");
            return Task.CompletedTask;
        }

        var saveDirPath = sec.ValidateWritePath(GetSavePath());
        if (saveDirPath == null)
        {
            GD.PrintErr($"[DataStoreAdapter] Invalid save directory path: {GetSavePath()}");
            return Task.CompletedTask;
        }

        DirAccess.MakeDirRecursiveAbsolute(saveDirPath.Value);

        var path = PathFor(key);
        var validatedPath = sec.ValidateWritePath(path);
        if (validatedPath == null)
        {
            GD.PrintErr($"[DataStoreAdapter] Write access denied: {path}");
            return Task.CompletedTask;
        }

        using var f = FileAccess.Open(validatedPath.Value, FileAccess.ModeFlags.Write);
        if (f != null)
        {
            f.StoreString(json);
            f.Flush();
        }
        return Task.CompletedTask;
    }

    private Task<string?> LoadFileAsync(string key)
    {
        var path = PathFor(key);
        var sec = GetSecurityFileAdapter();
        if (sec == null)
        {
            GD.PrintErr("[DataStoreAdapter] SecurityFileAdapter not initialized");
            return Task.FromResult<string?>(null);
        }

        var validatedPath = sec.ValidateReadPath(path);
        if (validatedPath == null)
        {
            GD.PrintErr($"[DataStoreAdapter] Read access denied: {path}");
            return Task.FromResult<string?>(null);
        }

        if (!FileAccess.FileExists(validatedPath.Value))
            return Task.FromResult<string?>(null);

        using var f = FileAccess.Open(validatedPath.Value, FileAccess.ModeFlags.Read);
        if (f == null) return Task.FromResult<string?>(null);
        return Task.FromResult<string?>(f.GetAsText());
    }

    private Task DeleteFileAsync(string key)
    {
        var path = PathFor(key);
        var sec = GetSecurityFileAdapter();
        if (sec == null)
        {
            GD.PrintErr("[DataStoreAdapter] SecurityFileAdapter not initialized");
            return Task.CompletedTask;
        }

        var validatedPath = sec.ValidateWritePath(path);
        if (validatedPath == null)
        {
            GD.PrintErr($"[DataStoreAdapter] Delete access denied: {path}");
            return Task.CompletedTask;
        }

        if (FileAccess.FileExists(validatedPath.Value))
        {
            DirAccess.RemoveAbsolute(validatedPath.Value);
        }
        return Task.CompletedTask;
    }
}
