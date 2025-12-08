using Game.Core.Contracts;
using Game.Core.Ports;

namespace Game.Core.Services;

/// <summary>
/// Security adapter for process execution with command whitelist.
/// Enforces command whitelist policy and blocks non-whitelisted execution.
/// </summary>
public class SecurityProcessAdapter
{
    private readonly IEventBus _eventBus;
    private readonly string[]? _allowedCommands;

    public SecurityProcessAdapter(IEventBus bus, string[]? allowedCommands = null)
    {
        _eventBus = bus;
        _allowedCommands = allowedCommands;
    }

    public bool IsCommandAllowed(string command)
    {
        if (_allowedCommands == null) return false;
        return _allowedCommands.Contains(command);
    }

    public async Task<ProcessExecuteResult?> ExecuteAsync(string command, string[] args)
    {
        if (!IsCommandAllowed(command))
        {
            await PublishDeniedEvent(command, args, "command_not_whitelisted");
            return null;
        }

        // Minimal implementation: whitelist check only
        // Real OS.execute integration would go here in production
        return null;
    }

    private async Task PublishDeniedEvent(string command, string[] args, string reason)
    {
        await _eventBus.PublishAsync(new DomainEvent(
            Type: "security.process.denied",
            Source: "SecurityProcessAdapter",
            Data: new
            {
                action = "execute_process",
                reason,
                target = command,
                arguments = string.Join(" ", args),
                caller = "SecurityProcessAdapter.ExecuteAsync"
            },
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString()
        ));
    }
}

/// <summary>
/// Result of process execution.
/// </summary>
public sealed record ProcessExecuteResult(
    int ExitCode,
    string Output,
    string Error
);
