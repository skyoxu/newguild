using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Game.Core.Contracts;
using Game.Core.Contracts.Security;

namespace Game.Godot.Adapters;

internal sealed class SecuritySnapshotAuditPublisher
{
    private readonly EventBusAdapter _bus;

    public SecuritySnapshotAuditPublisher(EventBusAdapter bus)
    {
        _bus = bus;
    }

    public bool Publish(string action, string reason, string target, string caller)
    {
        return PublishAsync(action, reason, target, caller).GetAwaiter().GetResult();
    }

    public async Task<bool> PublishAsync(string action, string reason, string target, string caller)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            GD.PushWarning("[SecuritySnapshotAuditPublisher] action is empty.");
            return false;
        }

        var source = string.IsNullOrWhiteSpace(caller) ? nameof(SecuritySnapshotAuditPublisher) : caller;
        var payload = JsonSerializer.Serialize(new SecuritySnapshotGateDecision(
            Ts: DateTimeOffset.UtcNow,
            Action: action,
            Reason: reason,
            Target: target,
            Caller: source));

        try
        {
            var evt = new DomainEvent(
                Type: SecuritySnapshotGateDecision.EventType,
                Source: source,
                Data: payload,
                Timestamp: DateTimeOffset.UtcNow,
                Id: Guid.NewGuid().ToString("N"));

            await _bus.PublishAsync(evt).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[SecuritySnapshotAuditPublisher] PublishAsync threw exType={ex.GetType().Name}");
            return TryWriteFallbackAudit(action, reason, target, source);
        }
    }

    internal static bool TryWriteFallbackAudit(string action, string reason, string target, string caller)
    {
        if (string.IsNullOrWhiteSpace(action))
            return false;

        try
        {
            var now = DateTimeOffset.UtcNow;
            var date = now.ToString("yyyy-MM-dd");
            var line = JsonSerializer.Serialize(new
            {
                ts = now.ToString("O"),
                action,
                reason,
                target,
                caller
            }) + System.Environment.NewLine;

            var userAuditPath = ResolveUserAuditPath(date);
            return TryAppendLine(userAuditPath, line);
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveUserAuditPath(string date)
    {
        var userLogDir = ProjectSettings.GlobalizePath($"user://logs/ci/{date}");
        return Path.Combine(userLogDir, "security-audit.jsonl");
    }

    private static bool TryAppendLine(string path, string line)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
                return false;

            Directory.CreateDirectory(directory);
            File.AppendAllText(path, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
