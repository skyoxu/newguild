using Godot;
using System;
using System.Collections.Generic;
using Game.Core.Ports;

namespace Game.Godot.Adapters.Db;

public partial class GodotSQLiteDatabaseErrorProbe : Node
{
    public global::Godot.Collections.Dictionary RunOpenAsyncAndCapture(
        string dbPath,
        bool secureMode,
        bool ciMode,
        string auditLogRootAbsolute)
    {
        ConfigureEnvironment(secureMode, ciMode, auditLogRootAbsolute);

        try
        {
            var db = new GodotSQLiteDatabase(dbPath);
            db.OpenAsync();
            return Result(threw: false, message: string.Empty, hasInner: false);
        }
        catch (Exception ex)
        {
            return Result(threw: true, message: ex.Message, hasInner: ex.InnerException != null);
        }
    }

    public global::Godot.Collections.Dictionary RunExecuteNonQueryAsyncAndCapture(
        string dbPath,
        string sql,
        bool secureMode,
        bool ciMode,
        string auditLogRootAbsolute)
    {
        ConfigureEnvironment(secureMode, ciMode, auditLogRootAbsolute);

        GodotSQLiteDatabase? db = null;
        try
        {
            db = new GodotSQLiteDatabase(dbPath);
            db.OpenAsync();
            var stmt = SqlStatement.WithParameters(
                sql,
                new Dictionary<string, object?>
                {
                    ["@P0"] = "p@ss"
                });
            db.ExecuteNonQueryAsync(stmt);
            return Result(threw: false, message: string.Empty, hasInner: false);
        }
        catch (Exception ex)
        {
            return Result(threw: true, message: ex.Message, hasInner: ex.InnerException != null);
        }
        finally
        {
            try { db?.CloseAsync(); } catch { }
        }
    }

    private static void ConfigureEnvironment(bool secureMode, bool ciMode, string auditLogRootAbsolute)
    {
        Environment.SetEnvironmentVariable("GD_SECURE_MODE", secureMode ? "1" : "0");
        Environment.SetEnvironmentVariable("CI", ciMode ? "1" : null);
        Environment.SetEnvironmentVariable("AUDIT_LOG_ROOT", auditLogRootAbsolute);
    }

    private static global::Godot.Collections.Dictionary Result(bool threw, string message, bool hasInner)
    {
        return new global::Godot.Collections.Dictionary
        {
            ["threw"] = threw,
            ["message"] = message,
            ["has_inner"] = hasInner,
        };
    }
}
