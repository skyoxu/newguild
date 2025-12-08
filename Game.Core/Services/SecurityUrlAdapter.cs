using Game.Core.Contracts;
using Game.Core.Ports;

namespace Game.Core.Services;

/// <summary>
/// Security adapter for URL validation with audit logging.
/// Enforces HTTPS-only whitelist policy and blocks dangerous schemes.
/// </summary>
public class SecurityUrlAdapter
{
    private readonly IEventBus _eventBus;
    private readonly string[]? _allowedDomains;

    public SecurityUrlAdapter(IEventBus bus, string[]? allowedDomains = null)
    {
        _eventBus = bus;
        _allowedDomains = allowedDomains;
    }

    public async Task<bool> ValidateAsync(string url)
    {
        Uri uri;
        try
        {
            uri = new Uri(url);
        }
        catch
        {
            await PublishDeniedEvent(url, "invalid_uri");
            return false;
        }

        // Block dangerous schemes (ADR-0019)
        if (uri.Scheme == "javascript" || uri.Scheme == "data" ||
            uri.Scheme == "blob" || uri.Scheme == "file")
        {
            await PublishDeniedEvent(url, $"{uri.Scheme}_scheme_blocked");
            return false;
        }

        // Domain whitelist enforcement
        if (_allowedDomains != null)
        {
            // Enforce HTTPS when whitelist is enabled
            if (uri.Scheme != "https")
            {
                await PublishDeniedEvent(url, "non_https_scheme");
                return false;
            }

            // Check domain whitelist
            if (!_allowedDomains.Contains(uri.Host))
            {
                await PublishDeniedEvent(url, "domain_not_whitelisted");
                return false;
            }
        }

        return true;
    }

    private async Task PublishDeniedEvent(string url, string reason)
    {
        await _eventBus.PublishAsync(new DomainEvent(
            Type: "security.url_access.denied",
            Source: "SecurityUrlAdapter",
            Data: new
            {
                action = "validate_url",
                reason,
                target = url,
                caller = "SecurityUrlAdapter.ValidateAsync"
            },
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString()
        ));
    }
}
