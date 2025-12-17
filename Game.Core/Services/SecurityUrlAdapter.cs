using Game.Core.Contracts;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Game.Core.Services;

/// <summary>
/// Core service for URL security validation with event-driven architecture.
/// Validates URLs against dangerous schemes and optional domain whitelist.
/// Publishes security.url_access.denied events when validation fails.
/// </summary>
public class SecurityUrlAdapter
{
    private readonly IEventBus _bus;
    private readonly string[]? _allowedDomains;

    private static readonly string[] DangerousSchemes = new[]
    {
        "javascript:",
        "file:",
        "data:",
        "blob:"
    };

    public SecurityUrlAdapter(InMemoryEventBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _allowedDomains = null;
    }

    public SecurityUrlAdapter(InMemoryEventBus bus, string[] allowedDomains)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _allowedDomains = allowedDomains ?? throw new ArgumentNullException(nameof(allowedDomains));
    }

    /// <summary>
    /// Validates URL against security policies.
    /// Returns true if URL is safe, false if rejected.
    /// Publishes security.url_access.denied event on rejection.
    /// </summary>
    public async Task<bool> ValidateAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            await PublishDeniedEventAsync(url ?? string.Empty, "URL is null or empty");
            return false;
        }

        // Check for dangerous schemes
        var lowerUrl = url.ToLowerInvariant();
        foreach (var scheme in DangerousSchemes)
        {
            if (lowerUrl.StartsWith(scheme))
            {
                await PublishDeniedEventAsync(url, $"Dangerous scheme detected: {scheme}");
                return false;
            }
        }

        // If domain whitelist configured, validate domain
        if (_allowedDomains != null && _allowedDomains.Length > 0)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                await PublishDeniedEventAsync(url, "Invalid URI format");
                return false;
            }

            // Enforce HTTPS-only when whitelist is configured (ADR-0019)
            if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                await PublishDeniedEventAsync(url, $"Non-HTTPS scheme rejected: {uri.Scheme}");
                return false;
            }

            var host = uri.Host.ToLowerInvariant();
            if (!_allowedDomains.Any(domain => host.Equals(domain, StringComparison.OrdinalIgnoreCase)))
            {
                await PublishDeniedEventAsync(url, $"Domain not in whitelist: {host}");
                return false;
            }
        }

        return true;
    }

    private async Task PublishDeniedEventAsync(string url, string reason)
    {
        var evt = new DomainEvent(
            Type: "security.url_access.denied",
            Source: "SecurityUrlAdapter",
            Data: new { Url = url, Reason = reason },
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString()
        );
        await _bus.PublishAsync(evt);
    }
}
