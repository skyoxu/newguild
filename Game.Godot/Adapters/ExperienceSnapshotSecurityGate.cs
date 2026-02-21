using System;
using System.Collections.Generic;
using System.Text.Json;
using Game.Core.Contracts.Engine;
using Game.Core.Contracts.Guild;
using Game.Core.Contracts.Media;
using Game.Core.Contracts.Progression;
using Game.Core.Contracts.Raid;
using Game.Core.Contracts.Recruitment;
using Game.Core.Contracts.Security;
using Godot;

namespace Game.Godot.Adapters;

internal sealed class ExperienceSnapshotSecurityGate
{
    private const string AllowMissingGuildContextEnv = "GD_SNAPSHOT_ALLOW_MISSING_GUILD_CONTEXT";
    private const string SecurityProfileEnv = "SECURITY_PROFILE";
    private const string SecurityProfileStrict = "strict";
    private const string SecurityProfileHostSafe = "host-safe";
    private const int MaxSnapshotPayloadLength = 8192;

    private static readonly JsonDocumentOptions SnapshotJsonOptions = new()
    {
        MaxDepth = 16
    };
    private static readonly string[] DefaultTrustedSources =
    {
        ExperienceChanged.SourceCore,
        ExperienceChanged.SourceUi,
        ExperienceChanged.SourceRewardLedger
    };
    private static readonly string[] DefaultTrustedSnapshotSourceEventTypes =
    {
        ExperienceChanged.EventType,
        LevelChanged.EventType,
        RaidResolved.EventType,
        GuildCreated.EventType,
        MediaBeatTriggered.EventType,
        RecruitmentOfferResolved.EventType,
        ReputationChanged.EventType,
        ScoreChanged.EventType
    };

    private readonly Func<Node?> _guildManagerResolver;
    private readonly Func<string, bool> _isTrustedEventId;
    private readonly HashSet<string> _trustedSources;
    private readonly HashSet<string> _trustedSnapshotSourceEventTypes;

    public ExperienceSnapshotSecurityGate(
        Func<Node?> guildManagerResolver,
        Func<string, bool> isTrustedEventId,
        IEnumerable<string> trustedSources,
        IEnumerable<string> trustedSnapshotSourceEventTypes)
    {
        _guildManagerResolver = guildManagerResolver;
        _isTrustedEventId = isTrustedEventId;
        _trustedSources = new HashSet<string>(trustedSources, StringComparer.Ordinal);
        _trustedSnapshotSourceEventTypes = new HashSet<string>(trustedSnapshotSourceEventTypes, StringComparer.Ordinal);
    }

    public static ExperienceSnapshotSecurityGate CreateDefault(
        Func<Node?> guildManagerResolver,
        Func<string, bool> isTrustedEventId)
    {
        return new ExperienceSnapshotSecurityGate(
            guildManagerResolver,
            isTrustedEventId,
            DefaultTrustedSources,
            DefaultTrustedSnapshotSourceEventTypes);
    }

    public bool TryValidateIngressSnapshot(string dataJson, string source, string eventId, out string rejectionReason)
    {
        rejectionReason = SecuritySnapshotGateDecision.ReasonNormalizeFailed;

        if (string.IsNullOrWhiteSpace(dataJson) || dataJson.Length > MaxSnapshotPayloadLength)
            return false;

        if (IsStrictProfile() && (!IsTrustedExperienceSource(source) || !_isTrustedEventId(eventId)))
        {
            rejectionReason = SecuritySnapshotGateDecision.ReasonUntrustedSource;
            return false;
        }

        return HasTrustedSnapshotSourceAndSessionGuildConsistency(dataJson, out rejectionReason);
    }

    public bool TryValidateLoadSnapshot(string normalizedPayload, out string rejectionReason)
    {
        rejectionReason = SecuritySnapshotGateDecision.ReasonNormalizeFailed;
        if (string.IsNullOrWhiteSpace(normalizedPayload))
            return false;

        return HasTrustedSnapshotSourceAndSessionGuildConsistency(normalizedPayload, out rejectionReason);
    }

    private bool IsTrustedExperienceSource(string source)
    {
        return !string.IsNullOrWhiteSpace(source) && _trustedSources.Contains(source);
    }

    private bool HasTrustedSnapshotSourceAndSessionGuildConsistency(string dataJson, out string rejectionReason)
    {
        rejectionReason = SecuritySnapshotGateDecision.ReasonGuildMismatch;

        if (!TryExtractSnapshotFields(dataJson, out var payloadGuildId, out var sourceEventType))
        {
            rejectionReason = SecuritySnapshotGateDecision.ReasonNormalizeFailed;
            return false;
        }

        if (!IsStrictProfile())
            return true;

        if (!IsTrustedSnapshotSourceEventType(sourceEventType))
        {
            rejectionReason = SecuritySnapshotGateDecision.ReasonUntrustedSource;
            return false;
        }

        if (!TryResolveSessionGuildId(out var sessionGuildId))
        {
            rejectionReason = SecuritySnapshotGateDecision.ReasonGuildContextMissing;
            return IsMissingGuildContextAllowed();
        }

        return string.Equals(payloadGuildId, sessionGuildId, StringComparison.Ordinal);
    }

    private static bool IsStrictProfile()
    {
        var profile = ResolveSecurityProfile();
        return string.Equals(profile, SecurityProfileStrict, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveSecurityProfile()
    {
        var profile = OS.GetEnvironment(SecurityProfileEnv);
        if (string.IsNullOrWhiteSpace(profile))
            profile = System.Environment.GetEnvironmentVariable(SecurityProfileEnv);

        if (!string.IsNullOrWhiteSpace(profile))
            return profile;

        if (string.Equals(OS.GetEnvironment("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal)
            || string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal))
        {
            return SecurityProfileStrict;
        }

        return SecurityProfileHostSafe;
    }

    private bool IsTrustedSnapshotSourceEventType(string sourceEventType)
    {
        return !string.IsNullOrWhiteSpace(sourceEventType)
               && _trustedSnapshotSourceEventTypes.Contains(sourceEventType);
    }

    private bool TryResolveSessionGuildId(out string sessionGuildId)
    {
        sessionGuildId = string.Empty;
        var guildManager = _guildManagerResolver();
        if (guildManager == null)
            return false;

        if (!guildManager.HasMethod("HasCurrentGuild") || !guildManager.HasMethod("GetCurrentGuildSummaryJson"))
            return false;

        var hasCurrentGuildVariant = guildManager.Call("HasCurrentGuild");
        if (hasCurrentGuildVariant.VariantType != Variant.Type.Bool || !hasCurrentGuildVariant.AsBool())
            return false;

        var summaryVariant = guildManager.Call("GetCurrentGuildSummaryJson");
        if (summaryVariant.VariantType != Variant.Type.String)
            return false;

        var summaryJson = summaryVariant.AsString();
        if (string.IsNullOrWhiteSpace(summaryJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(summaryJson, SnapshotJsonOptions);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (!root.TryGetProperty("guildId", out var guildIdElement)
                || guildIdElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var guildId = guildIdElement.GetString();
            if (string.IsNullOrWhiteSpace(guildId))
                return false;

            sessionGuildId = guildId;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsMissingGuildContextAllowed()
    {
        if (!OS.IsDebugBuild())
            return false;

        var allowMissingContext = string.Equals(OS.GetEnvironment(AllowMissingGuildContextEnv), "1", StringComparison.Ordinal)
                                  || string.Equals(System.Environment.GetEnvironmentVariable(AllowMissingGuildContextEnv), "1", StringComparison.Ordinal);
        if (!allowMissingContext)
            return false;

        return string.Equals(OS.GetEnvironment("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal)
               || string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal);
    }

    private static bool TryExtractSnapshotFields(string dataJson, out string guildId, out string sourceEventType)
    {
        guildId = string.Empty;
        sourceEventType = string.Empty;
        if (string.IsNullOrWhiteSpace(dataJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(dataJson, SnapshotJsonOptions);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (!TryReadString(root, "guildId", out guildId))
                return false;

            return TryReadString(root, "sourceEventType", out sourceEventType);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        value = text;
        return true;
    }
}
