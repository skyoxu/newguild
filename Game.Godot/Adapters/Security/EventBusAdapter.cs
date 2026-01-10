using Godot;
using Game.Core.Services;
using Game.Core.Ports;

namespace Game.Godot.Adapters.Security;

/// <summary>
/// Godot-compatible wrapper for InMemoryEventBus.
/// Allows GDScript tests to create and use event bus instances.
/// Inherits from RefCounted to enable GDScript preload/instantiation.
/// </summary>
public partial class EventBusAdapter : RefCounted
{
    private readonly InMemoryEventBus _bus;

    /// <summary>
    /// Parameterless constructor for Godot GDScript compatibility.
    /// Required for GDScript .new() instantiation.
    /// </summary>
    public EventBusAdapter() : this(null) { }

    /// <summary>
    /// Creates a new event bus adapter with optional logger.
    /// </summary>
    /// <param name="logger">Optional logger for event diagnostics</param>
    public EventBusAdapter(ILogger? logger = null)
    {
        _bus = new InMemoryEventBus(logger);
    }

    /// <summary>
    /// Gets the underlying InMemoryEventBus instance.
    /// Used by factory methods to pass bus to SecurityFileAdapter.
    /// </summary>
    public InMemoryEventBus GetBus()
    {
        return _bus;
    }

    /// <summary>
    /// GDScript-friendly accessor for the bus (returns self for compatibility).
    /// Factories will call GetBus() to retrieve the actual InMemoryEventBus.
    /// </summary>
    public EventBusAdapter Bus => this;
}
