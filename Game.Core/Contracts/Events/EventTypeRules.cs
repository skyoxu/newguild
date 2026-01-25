using System;
using System.Text.RegularExpressions;

namespace Game.Core.Contracts.Events;

/// <summary>
/// Single source of truth for domain event type format validation (ADR-0004).
/// </summary>
internal static class EventTypeRules
{
    private static readonly Regex EventTypeRegex = new(
        @"^[a-z0-9_]+(\.[a-z0-9_]+){2,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void Validate(string eventType, string paramName)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type is required.", paramName);
        if (!EventTypeRegex.IsMatch(eventType))
            throw new ArgumentException("Event type format is invalid.", paramName);
    }
}

