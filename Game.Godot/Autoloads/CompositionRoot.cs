using Game.Core.Ports;
using Godot;

namespace Game.Godot.Autoloads;

/// <summary>
/// Composition root for adapter layer. Provides port implementations
/// backed by Godot APIs and wires global event bus/logging.
/// Configure this class as an Autoload (Singleton) in project.godot.
/// </summary>
public partial class CompositionRoot : Node
{
    public static CompositionRoot? Instance { get; private set; }

    public ITime Time { get; private set; } = default!;
    public IInput Input { get; private set; } = default!;
    public IResourceLoader ResourceLoader { get; private set; } = default!;
    public IDataStore DataStore { get; private set; } = default!;
    public ILogger Logger { get; private set; } = default!;
    public IEventBus EventBus { get; private set; } = default!;

    private bool _initialized;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        if (_initialized)
            return;

        InitializePorts();
        _initialized = true;
    }

    private void InitializePorts()
    {
        // Prefer root-level autoload singletons if present; create minimal fallbacks if missing.
        var root = GetTree().Root;

        var bus = GetNodeOrNull<Adapters.EventBusAdapter>("/root/EventBus");
        if (bus == null)
        {
            bus = new Adapters.EventBusAdapter { Name = "EventBus" };
            root.AddChild(bus);
        }
        EventBus = bus;

        var time = GetNodeOrNull<Adapters.TimeAdapter>("/root/Time");
        if (time == null)
        {
            time = new Adapters.TimeAdapter { Name = "Time" };
            root.AddChild(time);
        }
        Time = time;

        var input = GetNodeOrNull<Adapters.InputAdapter>("/root/Input");
        if (input == null)
        {
            input = new Adapters.InputAdapter { Name = "Input" };
            root.AddChild(input);
        }
        Input = input;

        var logger = GetNodeOrNull<Adapters.LoggerAdapter>("/root/Logger");
        if (logger == null)
        {
            logger = new Adapters.LoggerAdapter { Name = "Logger" };
            root.AddChild(logger);
        }
        Logger = logger;

        var store = GetNodeOrNull<Adapters.DataStoreAdapter>("/root/DataStore");
        if (store == null)
        {
            store = new Adapters.DataStoreAdapter { Name = "DataStore" };
            root.AddChild(store);
        }
        DataStore = store;

        var loader = GetNodeOrNull<Adapters.ResourceLoaderAdapter>("/root/ResourceLoader");
        if (loader == null)
        {
            loader = new Adapters.ResourceLoaderAdapter { Name = "ResourceLoader" };
            root.AddChild(loader);
        }
        ResourceLoader = loader;
    }

    // Expose a simple status map for GDScript without accessing C# properties directly
    public global::Godot.Collections.Dictionary PortsStatus()
    {
        var d = new global::Godot.Collections.Dictionary
        {
            { "time", Time != null },
            { "input", Input != null },
            { "resourceLoader", ResourceLoader != null },
            { "dataStore", DataStore != null },
            { "logger", Logger != null },
            { "eventBus", EventBus != null },
        };
        return d;
    }
}
