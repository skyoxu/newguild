namespace Game.Core.Interfaces;

/// <summary>
/// Security validator interface for process execution control.
/// Implements command whitelist validation and argument sanitization.
/// </summary>
public interface ISecurityProcessValidator
{
    /// <summary>
    /// Checks if a command and its arguments are allowed for execution.
    /// </summary>
    /// <param name="command">The command or executable path</param>
    /// <param name="arguments">Command-line arguments</param>
    /// <returns>True if execution is allowed, false otherwise</returns>
    bool IsExecutionAllowed(string command, string[] arguments);

    /// <summary>
    /// Validates command execution and writes audit log on rejection.
    /// </summary>
    /// <param name="command">The command or executable path</param>
    /// <param name="arguments">Command-line arguments</param>
    /// <param name="caller">Context identifier (test name, function name, etc.)</param>
    /// <returns>Tuple of (IsAllowed, RejectionReason)</returns>
    (bool IsAllowed, string? RejectionReason) ValidateAndAudit(
        string command,
        string[] arguments,
        string caller);

    /// <summary>
    /// Sanitizes command arguments by masking sensitive parameters.
    /// Prevents CWE-532: Information Exposure Through Log Files.
    /// </summary>
    /// <param name="arguments">Original command-line arguments</param>
    /// <returns>Sanitized arguments with sensitive values masked</returns>
    string SanitizeArguments(string[] arguments);
}
