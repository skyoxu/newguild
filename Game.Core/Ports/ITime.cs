namespace Game.Core.Ports;

public interface ITime
{
    /// Returns time elapsed since last update in seconds.
    double DeltaSeconds { get; }

    /// <summary>
    /// Current UTC time as a deterministic-injectable source.
    /// </summary>
    System.DateTimeOffset UtcNowOffset { get; }
}
