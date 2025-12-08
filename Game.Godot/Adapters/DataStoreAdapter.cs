using System.Threading.Tasks;
using Godot;
using Game.Core.Ports;
using Game.Core.Services;

namespace Game.Godot.Adapters;

public partial class DataStoreAdapter : Node, IDataStore
{
    private readonly SecurityFileAdapter _securityFileAdapter;

    public DataStoreAdapter(SecurityFileAdapter securityFileAdapter)
    {
        _securityFileAdapter = securityFileAdapter ?? throw new System.ArgumentNullException(nameof(securityFileAdapter));
    }

    private static string MakeSafe(string key)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            key = key.Replace(c, '_');
        return key;
    }

    private static string GetSavePath() => "user://saves";
    private static string PathFor(string key) => $"{GetSavePath()}/{MakeSafe(key)}.json";

    public Task SaveAsync(string key, string json)
    {
        // Validate save directory path
        var saveDirPath = _securityFileAdapter.ValidateWritePath(GetSavePath());
        if (saveDirPath == null)
        {
            GD.PrintErr($"[DataStoreAdapter] Invalid save directory path: {GetSavePath()}");
            return Task.CompletedTask;
        }

        DirAccess.MakeDirRecursiveAbsolute(saveDirPath.Value);

        var path = PathFor(key);
        var validatedPath = _securityFileAdapter.ValidateWritePath(path);
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

    public Task<string?> LoadAsync(string key)
    {
        var path = PathFor(key);
        var validatedPath = _securityFileAdapter.ValidateReadPath(path);
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

    public Task DeleteAsync(string key)
    {
        var path = PathFor(key);
        var validatedPath = _securityFileAdapter.ValidateWritePath(path);
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

    // Synchronous helpers for GDScript tests
    public void SaveSync(string key, string json) => SaveAsync(key, json).Wait();
    public string? LoadSync(string key) => LoadAsync(key).Result;
    public void DeleteSync(string key) => DeleteAsync(key).Wait();
}
