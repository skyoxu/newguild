using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Game.Core.Tests.Docs.Support;

internal static class TaskmasterJson
{
    public static JsonElement? FindTaskById(JsonElement root, string id)
    {
        var tasks = FindMasterTasksArray(root);
        if (tasks is null)
        {
            return null;
        }

        foreach (var task in tasks.Value.EnumerateArray())
        {
            if (TryGetString(task, "id", out var taskId) && string.Equals(taskId, id, StringComparison.Ordinal))
            {
                return task;
            }
        }

        return null;
    }

    public static JsonElement? FindBackRecordByTaskmasterId(JsonElement root, int taskmasterId)
    {
        var records = FindBackRecordsArray(root);
        if (records is null)
        {
            return null;
        }

        foreach (var record in records.Value.EnumerateArray())
        {
            if (TryGetInt(record, "taskmaster_id", out var id) && id == taskmasterId)
            {
                return record;
            }
        }

        return null;
    }

    public static IReadOnlyList<string> ReadStringList(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Array)
            {
                return prop.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                var raw = prop.GetString() ?? string.Empty;
                var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => p.Length > 0)
                    .ToArray();
                return parts;
            }
        }

        return Array.Empty<string>();
    }

    public static string? ReadString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }

        return null;
    }

    private static JsonElement? FindMasterTasksArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("master", out var master) && master.ValueKind == JsonValueKind.Object)
            {
                if (master.TryGetProperty("tasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array)
                {
                    return tasks;
                }
            }

            if (root.TryGetProperty("tasks", out var directTasks) && directTasks.ValueKind == JsonValueKind.Array)
            {
                return directTasks;
            }
        }

        return null;
    }

    private static JsonElement? FindBackRecordsArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("tasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array)
            {
                return tasks;
            }

            if (root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
            {
                return records;
            }
        }

        return null;
    }

    private static bool TryGetString(JsonElement element, string key, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(key, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = prop.GetString();
        return value is not null;
    }

    private static bool TryGetInt(JsonElement element, string key, out int value)
    {
        value = default;
        if (!element.TryGetProperty(key, out var prop))
        {
            return false;
        }

        if (prop.ValueKind == JsonValueKind.Number)
        {
            return prop.TryGetInt32(out value);
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            return int.TryParse(prop.GetString(), out value);
        }

        return false;
    }
}

