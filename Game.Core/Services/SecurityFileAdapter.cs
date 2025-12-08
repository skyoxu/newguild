using Game.Core.Contracts;
using Game.Core.Domain;
using Game.Core.Ports;

namespace Game.Core.Services;

/// <summary>
/// Security adapter for file path validation with audit logging.
/// Enforces res:// (read-only) and user:// (read-write) policy, blocks path traversal.
/// </summary>
public class SecurityFileAdapter
{
    private readonly IEventBus _eventBus;

    public SecurityFileAdapter(IEventBus bus)
    {
        _eventBus = bus;
    }

    public SafeResourcePath? ValidatePath(string path)
    {
        var safePath = SafeResourcePath.FromString(path);
        if (safePath == null)
        {
            PublishDeniedEvent(path, "invalid_path");
        }
        return safePath;
    }

    public SafeResourcePath? ValidateReadPath(string path)
    {
        return ValidatePath(path);  // Both res:// and user:// allow read
    }

    public SafeResourcePath? ValidateWritePath(string path)
    {
        var safePath = SafeResourcePath.FromString(path);
        if (safePath == null)
        {
            PublishDeniedEvent(path, "invalid_path");
            return null;
        }

        // Reject res:// for write operations (read-only)
        if (safePath.Type == PathType.ReadOnly)
        {
            PublishDeniedEvent(path, "readonly_path");
            return null;
        }

        return safePath;
    }

    private void PublishDeniedEvent(string path, string reason)
    {
        _eventBus.PublishAsync(new DomainEvent(
            Type: "security.file_access.denied",
            Source: "SecurityFileAdapter",
            Data: new
            {
                action = "validate_path",
                reason,
                target = path,
                caller = "SecurityFileAdapter"
            },
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString()
        )).Wait();
    }
}
