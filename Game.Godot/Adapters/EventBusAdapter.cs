using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Game.Core.Contracts;
using Game.Core.Services;

namespace Game.Godot.Adapters;

/// <summary>
/// Godot adapter bridging Core DomainEvent publishing to scene-level signals.
/// </summary>
/// <remarks>
/// When enabled, security audit JSONL files are written under <c>user://logs/</c> and can be archived
/// into the repo <c>logs/</c> folder by CI/tools (see <c>scripts/python/godot_userlog_manager.py</c>).
/// </remarks>
public partial class EventBusAdapter : Node, IEventBus
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private SecurityAuditWriter? _securityAudit;

    [Signal]
    public delegate void DomainEventEmittedEventHandler(string type, string source, string dataJson, string id, string specVersion, string dataContentType, string timestampIso);

    private readonly List<Func<DomainEvent, Task>> _handlers = new();
    private readonly object _gate = new();

    public override void _Ready()
    {
        _securityAudit = new SecurityAuditWriter();
        _securityAudit.Start();
    }

    public override void _ExitTree()
    {
        if (_securityAudit != null)
            _ = _securityAudit.DisposeAsync().AsTask();
        _securityAudit = null;
    }

    public Task PublishAsync(DomainEvent evt)
    {
        // Emit Godot signal for scene-level listeners
        var dataJson = evt.Data is string s ? (string.IsNullOrWhiteSpace(s) ? "{}" : s)
                                            : JsonSerializer.Serialize(evt.Data, JsonOptions);
        EmitSignal(SignalName.DomainEventEmitted, evt.Type, evt.Source, dataJson, evt.Id, evt.SpecVersion, evt.DataContentType, evt.Timestamp.ToString("o"));

        _securityAudit?.TryEnqueue(evt, dataJson);

        // Notify in-process subscribers
        List<Func<DomainEvent, Task>> snapshot;
        lock (_gate) snapshot = _handlers.ToList();
        return Task.WhenAll(snapshot.Select(h => SafeInvoke(h, evt)));
    }

    private static async Task SafeInvoke(Func<DomainEvent, Task> h, DomainEvent evt)
    {
        try { await h(evt); }
        catch (Exception ex)
        {
            if (OS.IsDebugBuild())
                GD.PrintErr($"[EventBusAdapter][DEBUG] handler failed type={evt.Type} exType={ex.GetType().Name}");
        }
    }

    public IDisposable Subscribe(Func<DomainEvent, Task> handler)
    {
        lock (_gate) _handlers.Add(handler);
        return new Unsubscriber(_handlers, handler, _gate);
    }

    // Simple publish for GDScript tests without needing DomainEvent construction
    public void PublishSimple(string type, string source, string data_json)
    {
        if (!IsPublishSimpleAllowed())
        {
            if (OS.IsDebugBuild())
                GD.PushWarning("[EventBusAdapter][DEBUG] PublishSimple denied (not in debug/test mode).");
            return;
        }

        if (string.IsNullOrWhiteSpace(type))
            return;

        if (string.IsNullOrWhiteSpace(source))
            source = "gdscript";

        if (string.IsNullOrWhiteSpace(data_json))
            data_json = "{}";

        var evt = new DomainEvent(type.Trim(), source.Trim(), data_json, DateTime.UtcNow, Guid.NewGuid().ToString("N"));
        _ = PublishAsync(evt);
    }

    private static bool IsPublishSimpleAllowed()
    {
        if (OS.IsDebugBuild())
            return true;

        return string.Equals(
            System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"),
            "1",
            StringComparison.Ordinal);
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly List<Func<DomainEvent, Task>> _list;
        private readonly Func<DomainEvent, Task> _handler;
        private readonly object _gate;
        private bool _disposed;

        public Unsubscriber(List<Func<DomainEvent, Task>> list, Func<DomainEvent, Task> handler, object gate)
        { _list = list; _handler = handler; _gate = gate; }

        public void Dispose()
        {
            if (_disposed) return;
            lock (_gate) _list.Remove(_handler);
            _disposed = true;
        }
    }
}
