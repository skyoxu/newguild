using Godot;
using Game.Core.Contracts.Security;
using Game.Core.Progression;
using System;

namespace Game.Godot.Adapters;

public sealed class ExperienceSnapshotAdapter
{
    private string? _latestExperienceSnapshotPayload;

    public void ObserveExperienceEvent(string dataJson, string snapshotKey, Action<string, string, string, string>? audit = null)
    {
        if (ExperienceSnapshotNormalizer.TryNormalize(dataJson, out var normalizedPayload))
        {
            _latestExperienceSnapshotPayload = normalizedPayload;
        }
        else
        {
            audit?.Invoke(SecuritySnapshotGateDecision.ActionInvalid, SecuritySnapshotGateDecision.ReasonNormalizeFailed, snapshotKey, nameof(ExperienceSnapshotAdapter));
        }
    }

    public bool TryPersistSnapshot(Node dataStore, string snapshotKey, Action<string, string, string, string>? audit = null)
    {
        var payloadToPersist = string.Empty;
        if (!string.IsNullOrWhiteSpace(_latestExperienceSnapshotPayload))
        {
            payloadToPersist = _latestExperienceSnapshotPayload;
        }
        else
        {
            var persistedPayload = DataStoreSyncAccessor.TryLoadString(dataStore, snapshotKey);
            if (string.IsNullOrWhiteSpace(persistedPayload))
                return true;

            if (ExperienceSnapshotNormalizer.TryNormalize(persistedPayload, out var normalizedPersistedPayload))
            {
                payloadToPersist = normalizedPersistedPayload;
            }
            else
            {
                audit?.Invoke(SecuritySnapshotGateDecision.ActionInvalid, SecuritySnapshotGateDecision.ReasonNormalizeFailed, snapshotKey, nameof(ExperienceSnapshotAdapter));
                var cleared = TryClearSnapshot(dataStore, snapshotKey);
                if (cleared)
                {
                    _latestExperienceSnapshotPayload = null;
                    audit?.Invoke(SecuritySnapshotGateDecision.ActionCleared, SecuritySnapshotGateDecision.ReasonNormalizeFailed, snapshotKey, nameof(ExperienceSnapshotAdapter));
                }
                else
                {
                    audit?.Invoke(SecuritySnapshotGateDecision.ActionClearFailed, SecuritySnapshotGateDecision.ReasonNormalizeFailed, snapshotKey, nameof(ExperienceSnapshotAdapter));
                }

                return cleared;
            }
        }

        if (string.IsNullOrWhiteSpace(payloadToPersist))
            return true;

        var saved = DataStoreSyncAccessor.TrySaveString(dataStore, snapshotKey, payloadToPersist);
        if (saved)
            _latestExperienceSnapshotPayload = payloadToPersist;
        return saved;
    }

    public bool TryLoadSnapshot(
        Node dataStore,
        string snapshotKey,
        out string normalizedPayload,
        out bool hasPersistedSnapshot,
        out bool clearedInvalidSnapshot,
        Action<string, string, string, string>? audit = null)
    {
        normalizedPayload = string.Empty;
        clearedInvalidSnapshot = false;

        var persistedPayload = DataStoreSyncAccessor.TryLoadString(dataStore, snapshotKey);
        hasPersistedSnapshot = !string.IsNullOrWhiteSpace(persistedPayload);
        if (!hasPersistedSnapshot)
            return true;

        var normalized = ExperienceSnapshotNormalizer.TryNormalize(persistedPayload, out normalizedPayload);
        if (normalized)
        {
            _latestExperienceSnapshotPayload = normalizedPayload;
        }
        else
        {
            audit?.Invoke(SecuritySnapshotGateDecision.ActionRestoreFailed, SecuritySnapshotGateDecision.ReasonNormalizeFailed, snapshotKey, nameof(ExperienceSnapshotAdapter));

            var cleared = TryClearSnapshot(dataStore, snapshotKey);
            if (cleared)
            {
                hasPersistedSnapshot = false;
                clearedInvalidSnapshot = true;
                _latestExperienceSnapshotPayload = null;
                audit?.Invoke(SecuritySnapshotGateDecision.ActionCleared, SecuritySnapshotGateDecision.ReasonNormalizeFailed, snapshotKey, nameof(ExperienceSnapshotAdapter));
            }
            else
            {
                audit?.Invoke(SecuritySnapshotGateDecision.ActionClearFailed, SecuritySnapshotGateDecision.ReasonNormalizeFailed, snapshotKey, nameof(ExperienceSnapshotAdapter));
            }

            return cleared;
        }

        return true;
    }

    public bool TryRestoreAndPublishSnapshot(
        Node dataStore,
        string snapshotKey,
        Action<string>? publishSnapshot,
        out bool hasPersistedSnapshot,
        out bool clearedInvalidSnapshot,
        Action<string, string, string, string>? audit = null)
    {
        var loaded = TryLoadSnapshot(dataStore, snapshotKey, out var normalizedPayload, out hasPersistedSnapshot, out clearedInvalidSnapshot, audit);
        if (!loaded)
            return false;

        if (hasPersistedSnapshot && !string.IsNullOrWhiteSpace(normalizedPayload) && publishSnapshot != null)
            publishSnapshot(normalizedPayload);

        return true;
    }

    public bool TryClearSnapshotForSecurityReject(
        Node dataStore,
        string snapshotKey,
        string rejectionReason,
        Action<string, string, string, string>? audit = null)
    {
        var reason = string.IsNullOrWhiteSpace(rejectionReason)
            ? SecuritySnapshotGateDecision.ReasonNormalizeFailed
            : rejectionReason;

        var cleared = TryClearSnapshot(dataStore, snapshotKey);
        if (cleared)
        {
            _latestExperienceSnapshotPayload = null;
            audit?.Invoke(SecuritySnapshotGateDecision.ActionCleared, reason, snapshotKey, nameof(ExperienceSnapshotAdapter));
            return true;
        }

        audit?.Invoke(SecuritySnapshotGateDecision.ActionClearFailed, reason, snapshotKey, nameof(ExperienceSnapshotAdapter));
        return false;
    }

    private static bool TryClearSnapshot(Node dataStore, string snapshotKey)
    {
        var cleared = DataStoreSyncAccessor.TrySaveString(dataStore, snapshotKey, string.Empty);
        if (!cleared)
            return false;

        var readBack = DataStoreSyncAccessor.TryLoadString(dataStore, snapshotKey);
        return string.IsNullOrWhiteSpace(readBack);
    }
}
