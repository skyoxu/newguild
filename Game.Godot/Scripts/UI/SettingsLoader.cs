using Godot;
using System;
using Game.Core.Ports;
using Game.Godot.Adapters;

namespace Game.Godot.Scripts.UI;

public partial class SettingsLoader : Node
{
    private const string UserId = "default";

    public override void _Ready()
    {
        var db = GetNodeOrNull<SqliteDataStore>("/root/SqlDb");
        if (db == null) return;
        try
        {
            var rows = db.Query(SqlStatement.Positional(
                "SELECT audio_volume, graphics_quality, language FROM settings WHERE user_id=@0;",
                UserId));
            if (rows.Count == 0) return;
            var row = rows[0];
            if (row.TryGetValue("audio_volume", out var volumeValue) && volumeValue != null)
            {
                ApplyVolume((float)Convert.ToSingle(volumeValue));
            }
            if (row.TryGetValue("language", out var languageValue) && languageValue != null)
            {
                ApplyLanguage(languageValue.ToString() ?? "");
            }
            if (row.TryGetValue("graphics_quality", out var qualityValue) && qualityValue != null)
            {
                ApplyGraphicsQuality(qualityValue.ToString() ?? "medium");
            }
        }
        catch { }
    }

    private void ApplyVolume(float vol)
    {
        int bus = AudioServer.GetBusIndex("Master");
        if (bus >= 0) AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Clamp(vol,0,1)));
    }

    private void ApplyLanguage(string lang)
    {
        if (!string.IsNullOrEmpty(lang)) TranslationServer.SetLocale(lang);
    }

    private void ApplyGraphicsQuality(string q)
    {
        q = (q ?? "medium").ToLowerInvariant();
        try { DisplayServer.WindowSetVsyncMode(q == "low" ? DisplayServer.VSyncMode.Disabled : DisplayServer.VSyncMode.Enabled); } catch { }
        var vp = GetViewport();
        if (vp != null)
        {
            int msaa = q == "low" ? 0 : q == "medium" ? 1 : 2;
            try { vp.Set("msaa_2d", msaa); } catch { }
            try { vp.Set("msaa_3d", msaa); } catch { }
        }
    }
}
