using System;
using System.Text.RegularExpressions;

namespace Game.Core.Domain.Turn;

/// <summary>
/// Value object representing a validated save identifier.
/// </summary>
/// <remarks>
/// SaveId follows ADR-0019 security baseline: it is a logical identifier
/// for a long-running save slot and must not be used as a raw path or SQL fragment.
/// Allowed characters are [a-zA-Z0-9_-] with a maximum length of 64.
/// </remarks>
public sealed record SaveIdValue
{
    private static readonly Regex ValidPattern =
        new("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.Compiled);

    /// <summary>
    /// The validated identifier string.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new validated SaveIdValue instance.
    /// </summary>
    /// <param name="value">Raw save identifier string.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is null, whitespace, too long or contains invalid characters.
    /// </exception>
    public SaveIdValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SaveId cannot be null or whitespace.", nameof(value));

        if (!ValidPattern.IsMatch(value))
            throw new ArgumentException(
                "SaveId must match pattern [a-zA-Z0-9_-]{1,64}.",
                nameof(value));

        Value = value;
    }

    /// <summary>
    /// Creates a validated SaveIdValue instance if possible.
    /// </summary>
    /// <param name="value">Raw save identifier string.</param>
    /// <param name="result">Validated SaveIdValue when successful.</param>
    /// <returns>True when the value is valid; otherwise false.</returns>
    public static bool TryCreate(string value, out SaveIdValue? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!ValidPattern.IsMatch(value))
            return false;

        result = new SaveIdValue(value);
        return true;
    }

    /// <summary>
    /// Implicit conversion to string for convenience.
    /// </summary>
    public static implicit operator string(SaveIdValue value) => value.Value;

    /// <inheritdoc />
    public override string ToString() => Value;
}

