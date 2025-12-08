using Game.Core.Ports;
using Game.Core.Services;
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
        if (!_initialized)
        {
            InitializeAdapters();
            _initialized = true;
        }
    }

    public override void _Ready()
    {
        if (_initialized) return;
        InitializeAdapters();
        _initialized = true;
    }

    private void InitializeAdapters()
    {
        // Create EventBus first - security adapters need it for audit logging
        var busNode = new Adapters.EventBusAdapter();
        AddChild(busNode);
        EventBus = busNode;

        // Create security adapters (require EventBus)
        var securityFileAdapter = new SecurityFileAdapter(busNode);

        // Create other adapter nodes as children to ensure lifecycle managed by scene tree
        var time = new Adapters.TimeAdapter();
        var input = new Adapters.InputAdapter();
        var loader = new Adapters.ResourceLoaderAdapter();
        var store = new Adapters.DataStoreAdapter(securityFileAdapter);
        var logger = new Adapters.LoggerAdapter();

        AddChild(time);
        AddChild(input);
        AddChild(loader);
        AddChild(store);
        AddChild(logger);

        Time = time;
        Input = input;
        ResourceLoader = loader;
        DataStore = store;
        Logger = logger;
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
