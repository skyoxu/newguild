using System;
using System.Collections.Generic;
using Game.Core.Interfaces;
using Godot;

namespace Game.Godot.Adapters.Security;

/// <summary>
/// GDScript-compatible factory for creating SecurityUrlAdapter instances.
/// Provides instance methods that can be called from GDScript after instantiation.
/// </summary>
/// <remarks>
/// Design Rationale (ADR-0007 + Godot 4.x C# Interop):
/// - Godot 4.x GDScript cannot call static methods on CSharpScript from preload()
/// - Changed from static class to instance class to enable GDScript .new() pattern
/// - This factory maintains the security contract while providing GDScript interop
/// </remarks>
public partial class SecurityUrlAdapterFactory : RefCounted
{
    /// <summary>
    /// Creates SecurityUrlAdapter with specified whitelist configuration.
    /// </summary>
    /// <param name="allowedHosts">
    /// Whitelist of allowed HTTPS domain names. When null or empty, ALL external URLs are rejected
    /// to prevent SSRF attacks (CWE-918, CVSS 8.6). Required for production use.
    /// </param>
    /// <param name="auditLogPath">
    /// Optional path to security audit JSONL file. Defaults to logs/ci/{date}/security-audit.jsonl.
    /// </param>
    /// <returns>Configured SecurityUrlAdapter instance</returns>
    public SecurityUrlAdapter CreateWithWhitelist(string[] allowedHosts, string? auditLogPath = null)
    {
        return new SecurityUrlAdapter(allowedHosts, auditLogPath);
    }

    /// <summary>
    /// Creates SecurityUrlAdapter with SSRF protection (null whitelist).
    /// This configuration rejects ALL external URLs as secure default behavior.
    /// </summary>
    /// <param name="auditLogPath">Optional audit log path</param>
    /// <returns>Configured SecurityUrlAdapter with SSRF protection enabled</returns>
    public SecurityUrlAdapter CreateWithSsrfProtection(string? auditLogPath = null)
    {
        return new SecurityUrlAdapter(null, auditLogPath);
    }

    /// <summary>
    /// Creates SecurityUrlAdapter for testing with commonly used test domains.
    /// WARNING: FOR TESTING ONLY - Do not use in production code.
    /// </summary>
    /// <param name="auditLogPath">Optional audit log path for test verification</param>
    /// <returns>SecurityUrlAdapter configured with test whitelist</returns>
    public SecurityUrlAdapter CreateForTesting(string? auditLogPath = null)
    {
        var testWhitelist = new[] { "example.com", "api.example.com", "test.example.com" };
        return new SecurityUrlAdapter(testWhitelist, auditLogPath);
    }

    /// <summary>
    /// Helper method to convert Godot Array to C# string array for GDScript interop.
    /// </summary>
    /// <param name="godotArray">Godot Array containing string hostnames</param>
    /// <param name="auditLogPath">Optional audit log path</param>
    /// <returns>Configured SecurityUrlAdapter instance</returns>
    public SecurityUrlAdapter CreateFromGodotArray(global::Godot.Collections.Array godotArray, string? auditLogPath = null)
    {
        if (godotArray == null || godotArray.Count == 0)
        {
            return CreateWithSsrfProtection(auditLogPath);
        }

        var hostList = new List<string>();
        foreach (var item in godotArray)
        {
            if (item.VariantType != Variant.Type.Nil)
            {
                var hostname = item.AsString();
                if (!string.IsNullOrWhiteSpace(hostname))
                {
                    hostList.Add(hostname);
                }
            }
        }

        return new SecurityUrlAdapter(hostList, auditLogPath);
    }
}
