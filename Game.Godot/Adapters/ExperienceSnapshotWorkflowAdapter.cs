using System;
using System.Collections.Generic;
using Game.Core.Contracts.Security;
using Godot;

namespace Game.Godot.Adapters;

public sealed class ExperienceSnapshotWorkflowAdapter
{
    private const string DefaultAuditCaller = nameof(ExperienceSnapshotWorkflowAdapter);
    private readonly ExperienceSnapshotAdapter _snapshotAdapter = new();
    private readonly ExperienceSnapshotSecurityGate _securityGate;
    private readonly SecuritySnapshotAuditPublisher? _auditPublisher;

    private static readonly HashSet<string> TrustedSnapshotAuditCallers = new(StringComparer.Ordinal)
    {
        nameof(ExperienceSnapshotWorkflowAdapter),
        nameof(ExperienceSnapshotAdapter),
        nameof(ExperienceSnapshotSecurityGate),
        nameof(SecuritySnapshotAuditPublisher),
        "StartScreen"
    };

    public ExperienceSnapshotWorkflowAdapter(EventBusAdapter? bus, Func<Node?> guildManagerResolver)
    {
        _securityGate = ExperienceSnapshotSecurityGate.CreateDefault(
            guildManagerResolver,
            eventId => bus != null && bus.IsTrustedPublisherEvent(eventId));
        _auditPublisher = bus != null ? new SecuritySnapshotAuditPublisher(bus) : null;
    }

    public void HandleExperienceDomainEvent(string dataJson, string source, string eventId, string snapshotKey, string caller)
    {
        if (!_securityGate.TryValidateIngressSnapshot(dataJson, source, eventId, out var rejectionReason))
        {
            PublishAuditEvent(SecuritySnapshotGateDecision.ActionInvalid, rejectionReason, snapshotKey, caller);
            return;
        }

        _snapshotAdapter.ObserveExperienceEvent(dataJson, snapshotKey, PublishAuditEvent);
    }

    public bool TryPersistSnapshot(Node dataStore, string snapshotKey)
    {
        return _snapshotAdapter.TryPersistSnapshot(dataStore, snapshotKey, PublishAuditEvent);
    }

    public bool TryLoadSnapshot(
        Node dataStore,
        string snapshotKey,
        out string normalizedPayload,
        out bool hasPersistedSnapshot,
        out bool clearedInvalidSnapshot)
    {
        return _snapshotAdapter.TryLoadSnapshot(
            dataStore,
            snapshotKey,
            out normalizedPayload,
            out hasPersistedSnapshot,
            out clearedInvalidSnapshot,
            PublishAuditEvent);
    }

    public bool TryAcceptSnapshotForLoad(string normalizedPayload, out string rejectionReason)
    {
        return _securityGate.TryValidateLoadSnapshot(normalizedPayload, out rejectionReason);
    }

    public bool TryClearSnapshotForSecurityReject(Node dataStore, string snapshotKey, string rejectionReason)
    {
        return _snapshotAdapter.TryClearSnapshotForSecurityReject(dataStore, snapshotKey, rejectionReason, PublishAuditEvent);
    }

    public void PublishAuditEvent(string decision, string reason, string target, string caller)
    {
        if (string.IsNullOrWhiteSpace(decision))
            return;

        var normalizedCaller = NormalizeSnapshotAuditCaller(caller);
        if (_auditPublisher == null)
        {
            GD.PushWarning($"[ExperienceSnapshotWorkflowAdapter] Snapshot audit publisher unavailable action={decision} reason={reason} target={target}");
            _ = SecuritySnapshotAuditPublisher.TryWriteFallbackAudit(decision, reason, target, normalizedCaller);
            return;
        }

        var published = _auditPublisher.Publish(decision, reason, target, normalizedCaller);
        if (!published)
            GD.PushWarning($"[ExperienceSnapshotWorkflowAdapter] Snapshot audit publish failed action={decision} reason={reason} target={target}");
    }

    private static string NormalizeSnapshotAuditCaller(string caller)
    {
        if (!string.IsNullOrWhiteSpace(caller) && TrustedSnapshotAuditCallers.Contains(caller))
            return caller;

        return DefaultAuditCaller;
    }
}
