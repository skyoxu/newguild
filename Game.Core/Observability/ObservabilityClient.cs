using Game.Core.Ports;

namespace Game.Core.Observability;

/// <summary>
/// Lightweight, engine-agnostic entry point for observability capabilities in Game.Core.
/// </summary>
public sealed class ObservabilityClient
{
    public ObservabilityClient(ILogger logger)
    {
        Logger = logger;
    }

    public ILogger Logger { get; }
}

