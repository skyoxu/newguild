using Godot;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Game.Godot.Scripts.Security;

public partial class SecurityAudit : Node
{
    public override void _Ready()
    {
        try
        {
            bool hasSqlite = false;
            try
            {
                // Avoid engine error log by checking class list before probing
                var classes = ClassDB.GetClassList();
                foreach (var c in classes)
                {
                    var s = c.ToString();
                    if (s == "SQLite") { hasSqlite = true; break; }
                }
            }
            catch { hasSqlite = false; }

            var appName = GetAppNameSafe();
            var info = new
            {
                // ADR-0019 required fields for security-audit.jsonl
                ts = DateTime.UtcNow.ToString("o"),
                action = "security.baseline.checked",
                reason = "startup",
                target = appName,
                caller = nameof(SecurityAudit),

                // Extra diagnostics (allowed by validator)
                app = appName,
                godot = Engine.GetVersionInfo()["string"].ToString(),
                db_backend = System.Environment.GetEnvironmentVariable("GODOT_DB_BACKEND") ?? "default",
                demo = (System.Environment.GetEnvironmentVariable("TEMPLATE_DEMO") ?? "0").ToLowerInvariant() == "1",
                plugin_sqlite = hasSqlite,
            };

            var json = JsonSerializer.Serialize(info);

            var root = System.Environment.GetEnvironmentVariable("AUDIT_LOG_ROOT");
            string path;
            if (!string.IsNullOrWhiteSpace(root))
            {
                Directory.CreateDirectory(root);
                path = Path.Combine(root, "security-audit.jsonl");
            }
            else
            {
                var dir = ProjectSettings.GlobalizePath("user://logs/security");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, "security-audit.jsonl");
            }

            File.AppendAllText(path, json + System.Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[SecurityAudit] write failed: {ex.Message}");
        }
    }
    private static string GetAppNameSafe()
    {
        try
        {
            var v = ProjectSettings.GetSetting("application/config/name");
            return v.VariantType == Variant.Type.Nil ? "GodotGame" : v.AsString();
        }
        catch { return "GodotGame"; }
    }
}
