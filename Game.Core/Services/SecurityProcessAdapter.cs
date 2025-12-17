using System;
using System.Diagnostics;
using Game.Core.Contracts;

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
        // ADR-0019: Check if process execution is enabled (development mode only)
        // Production mode (default): GD_ENABLE_PROCESS_EXECUTION not set or != "1"
        // Development mode: GD_ENABLE_PROCESS_EXECUTION = "1"
        var executionEnabled = Environment.GetEnvironmentVariable("GD_ENABLE_PROCESS_EXECUTION") == "1";

        if (!executionEnabled)
        {
            // Production mode: reject all process execution attempts
            await PublishDeniedEvent(command, args, "process_execution_disabled_in_production");
            return null;
        }

        // Development mode: continue with whitelist check
        if (!IsCommandAllowed(command))
        {
            await PublishDeniedEvent(command, args, "command_not_whitelisted");
            return null;
        }

        // Development mode with whitelisted command: execute process
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                await PublishDeniedEvent(command, args, "process_start_failed");
                return null;
            }

            // Read output and error asynchronously
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            // Publish success audit event per ADR-0019
            await PublishApprovedEvent(command, args, process.ExitCode);

            return new ProcessExecuteResult(
                ExitCode: process.ExitCode,
                Output: output,
                Error: error
            );
        }
        catch (Exception ex)
        {
            // Log exception and publish denial event
            await PublishDeniedEvent(command, args, $"execution_exception: {ex.Message}");
            return null;
        }
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

    private async Task PublishApprovedEvent(string command, string[] args, int exitCode)
    {
        await _eventBus.PublishAsync(new DomainEvent(
            Type: "security.process.approved",
            Source: "SecurityProcessAdapter",
            Data: new
            {
                action = "execute_process",
                target = command,
                arguments = string.Join(" ", args),
                exitCode,
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
