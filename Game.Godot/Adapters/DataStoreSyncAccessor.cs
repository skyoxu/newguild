using Godot;

namespace Game.Godot.Adapters;

internal static class DataStoreSyncAccessor
{
    public static bool TrySaveString(Node dataStore, string key, string json)
    {
        if (dataStore.HasMethod("TrySaveSync"))
        {
            var result = dataStore.Call("TrySaveSync", key, json);
            return result.VariantType == Variant.Type.Bool && result.AsBool();
        }

        if (dataStore.HasMethod("SaveSync"))
        {
            var result = dataStore.Call("SaveSync", key, json);
            if (result.VariantType == Variant.Type.Bool)
                return result.AsBool();

            return result.VariantType == Variant.Type.Nil;
        }

        return false;
    }

    public static string? TryLoadString(Node dataStore, string key)
    {
        Variant loadedValue;
        if (dataStore.HasMethod("TryLoadSync"))
            loadedValue = (Variant)dataStore.Call("TryLoadSync", key);
        else if (dataStore.HasMethod("LoadSync"))
            loadedValue = (Variant)dataStore.Call("LoadSync", key);
        else
            return null;

        if (loadedValue.VariantType == Variant.Type.Nil)
            return null;

        var loadedText = loadedValue.AsString();
        return string.IsNullOrWhiteSpace(loadedText) ? null : loadedText;
    }
}
