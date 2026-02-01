using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
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
    private const int DefaultRecentEventsMax = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private SecurityAuditWriter? _securityAudit;
    private const int DefaultAuditFlushTimeoutMs = 250;
    private int _mainThreadId;
    private readonly ConcurrentQueue<PendingPublish> _pending = new();
    private int _flushScheduled;

    private sealed record PendingPublish(
        string Type,
        string Source,
        string DataJson,
        string Id,
        string SpecVersion,
        string DataContentType,
        string TimestampIso,
        TaskCompletionSource<bool> Completion);

    private sealed record RecentDomainEvent(
        string Type,
        string Source,
        string DataJson,
        string Id,
        string SpecVersion,
        string DataContentType,
        string TimestampIso);

    [Signal]
    public delegate void DomainEventEmittedEventHandler(string type, string source, string dataJson, string id, string specVersion, string dataContentType, string timestampIso);

    private readonly List<Func<DomainEvent, Task>> _handlers = new();
    private readonly object _gate = new();
    private readonly List<RecentDomainEvent> _recent = new();
    private readonly object _recentGate = new();

    public override void _Ready()
    {
        _mainThreadId = System.Environment.CurrentManagedThreadId;
        _securityAudit = new SecurityAuditWriter();
        _securityAudit.Start();
    }

    public override void _ExitTree()
    {
        if (_securityAudit != null)
        {
            var timeoutMs = GetAuditFlushTimeoutMs();
            var ok = _securityAudit.StopAndFlush(TimeSpan.FromMilliseconds(timeoutMs));
            if (!ok && IsAuditWarningsEnabled())
                GD.PushWarning($"[EventBusAdapter] security audit flush timed out after {timeoutMs}ms.");
        }
        _securityAudit = null;
    }

    public Task PublishAsync(DomainEvent evt)
    {
        var dataJson = evt.Data is string dataText ? (string.IsNullOrWhiteSpace(dataText) ? "{}" : dataText)
                                            : JsonSerializer.Serialize(evt.Data, JsonOptions);

        if (System.Environment.CurrentManagedThreadId == _mainThreadId)
            return PublishOnMainThread(evt, dataJson);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.Enqueue(new PendingPublish(
            Type: evt.Type,
            Source: evt.Source,
            DataJson: dataJson,
            Id: evt.Id,
            SpecVersion: evt.SpecVersion,
            DataContentType: evt.DataContentType,
            TimestampIso: evt.Timestamp.ToString("o"),
            Completion: tcs));

        if (Interlocked.Exchange(ref _flushScheduled, 1) == 0 && IsInsideTree())
            CallDeferred(nameof(FlushPending));

        return tcs.Task;
    }

    private void FlushPending()
    {
        Interlocked.Exchange(ref _flushScheduled, 0);

        while (_pending.TryDequeue(out var pending))
        {
            try
            {
                var ts = ParseTimestampOrNow(pending.TimestampIso);
                var evt = new DomainEvent(
                    Type: pending.Type,
                    Source: pending.Source,
                    Data: pending.DataJson,
                    Timestamp: ts,
                    Id: pending.Id,
                    SpecVersion: pending.SpecVersion,
                    DataContentType: pending.DataContentType);

                var task = PublishOnMainThread(evt, pending.DataJson);
                _ = task.ContinueWith(
                    t =>
                    {
                        if (t.IsFaulted)
                            pending.Completion.TrySetException(t.Exception?.GetBaseException() ?? new Exception("Publish failed."));
                        else if (t.IsCanceled)
                            pending.Completion.TrySetCanceled();
                        else
                            pending.Completion.TrySetResult(true);
                    },
                    TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                pending.Completion.TrySetException(ex);
            }
        }

        if (!_pending.IsEmpty && Interlocked.Exchange(ref _flushScheduled, 1) == 0 && IsInsideTree())
            CallDeferred(nameof(FlushPending));
    }

    private Task PublishOnMainThread(DomainEvent evt, string dataJson)
    {
        var timestampIso = evt.Timestamp.ToString("o");
        EmitSignal(SignalName.DomainEventEmitted, evt.Type, evt.Source, dataJson, evt.Id, evt.SpecVersion, evt.DataContentType, timestampIso);
        _securityAudit?.TryEnqueue(evt, dataJson);
        RememberRecent(evt.Type, evt.Source, dataJson, evt.Id, evt.SpecVersion, evt.DataContentType, timestampIso);

        List<Func<DomainEvent, Task>> snapshot;
        lock (_gate) snapshot = _handlers.ToList();
        return Task.WhenAll(snapshot.Select(h => SafeInvoke(h, evt)));
    }

    private void RememberRecent(string type, string source, string dataJson, string id, string specVersion, string dataContentType, string timestampIso)
    {
        lock (_recentGate)
        {
            _recent.Add(new RecentDomainEvent(type, source, dataJson, id, specVersion, dataContentType, timestampIso));
            if (_recent.Count > DefaultRecentEventsMax)
                _recent.RemoveRange(0, _recent.Count - DefaultRecentEventsMax);
        }
    }

    public IReadOnlyList<(string type, string source, string dataJson, string id, string specVersion, string dataContentType, string timestampIso)> GetRecentEvents(int max = DefaultRecentEventsMax)
    {
        if (max <= 0) return Array.Empty<(string, string, string, string, string, string, string)>();

        lock (_recentGate)
        {
            var take = Math.Min(Math.Max(0, max), _recent.Count);
            var slice = _recent.Skip(Math.Max(0, _recent.Count - take)).ToList();
            return slice.Select(x => (x.Type, x.Source, x.DataJson, x.Id, x.SpecVersion, x.DataContentType, x.TimestampIso)).ToList();
        }
    }

    public int GetRecentCount()
    {
        lock (_recentGate) return _recent.Count;
    }

    public Godot.Collections.Array GetRecentSignalArgs(int max = DefaultRecentEventsMax)
    {
        var arr = new Godot.Collections.Array();
        if (max <= 0) return arr;

        lock (_recentGate)
        {
            var take = Math.Min(Math.Max(0, max), _recent.Count);
            foreach (var e in _recent.Skip(Math.Max(0, _recent.Count - take)))
            {
                arr.Add(new Godot.Collections.Array
                {
                    e.Type,
                    e.Source,
                    e.DataJson,
                    e.Id,
                    e.SpecVersion,
                    e.DataContentType,
                    e.TimestampIso,
                });
            }
        }

        return arr;
    }

    private static DateTime ParseTimestampOrNow(string timestampIso)
    {
        if (DateTime.TryParse(timestampIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ts))
            return ts;
        return DateTime.UtcNow;
    }

    private static async Task SafeInvoke(Func<DomainEvent, Task> h, DomainEvent evt)
    {
        try { await h(evt).ConfigureAwait(false); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[EventBusAdapter] handler failed type={evt.Type} exType={ex.GetType().Name}");
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

        return string.Equals(OS.GetEnvironment("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal) ||
               string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal);
    }

    private static int GetAuditFlushTimeoutMs()
    {
        var raw = OS.GetEnvironment("SECURITY_AUDIT_FLUSH_TIMEOUT_MS")
                  ?? System.Environment.GetEnvironmentVariable("SECURITY_AUDIT_FLUSH_TIMEOUT_MS");

        if (int.TryParse(raw, out var ms))
            return Math.Clamp(ms, 0, 10_000);

        return DefaultAuditFlushTimeoutMs;
    }

    private static bool IsAuditWarningsEnabled()
    {
        if (OS.IsDebugBuild())
            return true;

        return string.Equals(OS.GetEnvironment("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal) ||
               string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal) ||
               string.Equals(System.Environment.GetEnvironmentVariable("CI"), "1", StringComparison.Ordinal) ||
               string.Equals(System.Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
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
