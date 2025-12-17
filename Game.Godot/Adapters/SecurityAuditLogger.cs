using Godot;
using Game.Core.Contracts;
using Game.Core.Ports;
using Game.Core.Services;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Game.Godot.Adapters;

/// <summary>
/// Adapter for explicit security audit logging per ADR-0019.
/// Subscribes to DomainEvent stream and writes security-relevant events to JSONL file.
/// </summary>
public partial class SecurityAuditLogger : Node
{
    private const string AuditLogPath = "user://logs/security-audit.jsonl";
    private readonly IEventBus _eventBus;
    private readonly SecurityFileAdapter _securityFileAdapter;
    private bool _isSubscribed;

    public SecurityAuditLogger(IEventBus eventBus, SecurityFileAdapter securityFileAdapter)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _securityFileAdapter = securityFileAdapter ?? throw new ArgumentNullException(nameof(securityFileAdapter));
    }

    public override void _Ready()
    {
        // Ensure logs directory exists
        EnsureLogDirectoryExists();

        // Subscribe to security-relevant events
        SubscribeToSecurityEvents();
    }

    public override void _ExitTree()
    {
        // Unsubscribe when node is removed
        if (_isSubscribed)
        {
            // Event bus cleanup handled automatically
            _isSubscribed = false;
        }
    }

    private void SubscribeToSecurityEvents()
    {
        if (_isSubscribed) return;

        // Subscribe to all events, filter in handler
        _eventBus.Subscribe(HandleSecurityEvent);
        _isSubscribed = true;

        GD.Print("[SecurityAuditLogger] Subscribed to security events");
    }

    private async Task HandleSecurityEvent(DomainEvent evt)
    {
        // Filter for security-relevant event types
        if (!IsSecurityRelevant(evt.Type))
            return;

        try
        {
            await WriteAuditEntryAsync(evt);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SecurityAuditLogger] Failed to write audit entry: {ex.Message}");
        }
    }

    private static bool IsSecurityRelevant(string eventType)
    {
        // Security-relevant event patterns per ADR-0019
        return eventType.Contains("save.") ||
               eventType.Contains("guild.") ||
               eventType.Contains("auth.") ||
               eventType.Contains("permission.") ||
               eventType.Contains("error.") ||
               eventType.Contains("security.");
    }

    private async Task WriteAuditEntryAsync(DomainEvent evt)
    {
        var auditEntry = new AuditLogEntry
        {
            Timestamp = evt.Timestamp,
            EventType = evt.Type,
            EventId = evt.Id,
            Source = evt.Source,
            Data = evt.Data?.ToString() ?? string.Empty
        };

        var json = JsonSerializer.Serialize(auditEntry, new JsonSerializerOptions
        {
            WriteIndented = false // JSONL format: one line per entry
        });

        // Validate audit log path before writing
        var validatedPath = _securityFileAdapter.ValidateWritePath(AuditLogPath);
        if (validatedPath == null)
        {
            GD.PrintErr($"[SecurityAuditLogger] Write access denied: {AuditLogPath}");
            return;
        }

        // Append to JSONL file (one JSON object per line)
        using var file = FileAccess.Open(validatedPath.Value, FileAccess.ModeFlags.ReadWrite);
        if (file != null)
        {
            file.SeekEnd();
            file.StoreLine(json);
        }

        await Task.CompletedTask;
    }

    private void EnsureLogDirectoryExists()
    {
        const string logDir = "user://logs";

        // Validate log directory path
        var validatedLogDir = _securityFileAdapter.ValidateWritePath(logDir);
        if (validatedLogDir == null)
        {
            GD.PrintErr($"[SecurityAuditLogger] Cannot validate log directory: {logDir}");
            return;
        }

        if (!DirAccess.DirExistsAbsolute(validatedLogDir.Value))
        {
            var validatedUserDir = _securityFileAdapter.ValidateWritePath("user://");
            if (validatedUserDir == null)
            {
                GD.PrintErr("[SecurityAuditLogger] Cannot validate user:// directory");
                return;
            }

            var dir = DirAccess.Open(validatedUserDir.Value);
            if (dir != null)
            {
                var err = dir.MakeDir("logs");
                if (err != Error.Ok)
                {
                    GD.PrintErr($"[SecurityAuditLogger] Failed to create logs directory: {err}");
                }
            }
        }
    }

    /// <summary>
    /// Audit log entry structure for JSONL format.
    /// </summary>
    private sealed record AuditLogEntry
    {
        public DateTime Timestamp { get; init; }
        public string EventType { get; init; } = string.Empty;
        public string EventId { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string Data { get; init; } = string.Empty;
    }
}
