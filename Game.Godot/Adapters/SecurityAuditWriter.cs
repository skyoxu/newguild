using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Game.Core.Contracts;
using Godot;

namespace Game.Godot.Adapters;

internal sealed class SecurityAuditWriter : IAsyncDisposable
{
    private const long DefaultMaxFileBytes = 5_000_000;
    private const int DefaultMaxTotalBytesMultiplier = 2;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly object _lifecycleGate = new();
    private Channel<AuditWriteRequest>? _channel;
    private Task? _worker;
    private readonly long _maxFileBytes;
    private readonly long _maxTotalBytes;
    private readonly object _fileGate = new();
    private CancellationTokenSource? _cts;
    private readonly Dictionary<string, string> _lastEntryHashByLogicalPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _touchedAuditDirectories = new(StringComparer.OrdinalIgnoreCase);

    private long _enqueued;
    private long _written;
    private long _dropped;
    private long _failed;
    private long _droppedBySize;

    public SecurityAuditWriter(long? maxFileBytes = null, long? maxTotalBytes = null)
    {
        _maxFileBytes = maxFileBytes ?? DefaultMaxFileBytes;
        _maxTotalBytes = maxTotalBytes ?? checked(_maxFileBytes * DefaultMaxTotalBytesMultiplier);
    }

    public void Start()
    {
        lock (_lifecycleGate)
        {
            if (_channel != null)
                return;

            _channel = Channel.CreateUnbounded<AuditWriteRequest>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });
            _cts = new CancellationTokenSource();
            var channel = _channel;
            _worker = Task.Run(() => RunAsync(channel, _cts.Token));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAndFlushAsync(timeout: TimeSpan.FromSeconds(2));
    }

    public bool TryEnqueue(DomainEvent evt, string dataJson)
    {
        if (!IsEnabled())
            return false;
        if (!evt.Type.StartsWith("security.", StringComparison.OrdinalIgnoreCase))
            return false;

        var now = DateTime.UtcNow;
        var ts = evt.Timestamp.ToUniversalTime();
        if (ts < now.AddDays(-1) || ts > now.AddDays(1))
            ts = now;

        var date = ts.ToString("yyyy-MM-dd");
        var logicalPath = $"user://logs/ci/{date}/security-audit.jsonl";
        var fullPath = ProjectSettings.GlobalizePath(logicalPath);

        var req = new AuditWriteRequest(evt, dataJson, logicalPath, fullPath, now);
        Channel<AuditWriteRequest>? channel;
        lock (_lifecycleGate) channel = _channel;
        if (channel == null)
        {
            Interlocked.Increment(ref _dropped);
            return false;
        }

        var accepted = channel.Writer.TryWrite(req);
        if (accepted) Interlocked.Increment(ref _enqueued);
        else Interlocked.Increment(ref _dropped);
        return accepted;
    }

    private static bool IsEnabled()
    {
        // Prefer Godot environment API so GdUnit tests using OS.set_environment are honored.
        var secureMode = OS.GetEnvironment("GD_SECURE_MODE");
        if (string.Equals(secureMode, "1", StringComparison.Ordinal))
            return true;

        var securityTestMode = OS.GetEnvironment("SECURITY_TEST_MODE");
        if (string.Equals(securityTestMode, "1", StringComparison.Ordinal))
            return true;

        if (string.Equals(System.Environment.GetEnvironmentVariable("GD_SECURE_MODE"), "1", StringComparison.Ordinal))
            return true;

        if (string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal))
            return true;

        var ci = System.Environment.GetEnvironmentVariable("CI");
        return string.Equals(ci, "1", StringComparison.Ordinal) ||
               string.Equals(ci, "true", StringComparison.OrdinalIgnoreCase);
    }

    public bool StopAndFlush(TimeSpan timeout)
    {
        Channel<AuditWriteRequest>? channel;
        Task? worker;
        CancellationTokenSource? cts;
        lock (_lifecycleGate)
        {
            channel = _channel;
            worker = _worker;
            cts = _cts;
            _channel = null;
            _worker = null;
            _cts = null;
        }

        if (channel != null)
            channel.Writer.TryComplete();

        if (worker == null)
            return true;

        try
        {
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                worker.Wait();
                WriteMetaFilesIfAny();
                return true;
            }

            var done = worker.Wait(timeout);
            if (done)
            {
                WriteMetaFilesIfAny();
                return true;
            }

            cts?.Cancel();
            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> StopAndFlushAsync(TimeSpan timeout)
    {
        Channel<AuditWriteRequest>? channel;
        Task? worker;
        CancellationTokenSource? cts;
        lock (_lifecycleGate)
        {
            channel = _channel;
            worker = _worker;
            cts = _cts;
            _channel = null;
            _worker = null;
            _cts = null;
        }

        if (channel != null)
            channel.Writer.TryComplete();

        if (worker == null)
            return true;

        try
        {
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                await worker.ConfigureAwait(false);
                WriteMetaFilesIfAny();
                return true;
            }

            var done = await Task.WhenAny(worker, Task.Delay(timeout)).ConfigureAwait(false) == worker;
            if (done)
            {
                WriteMetaFilesIfAny();
                return true;
            }

            if (cts != null)
                cts.Cancel();

            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task RunAsync(Channel<AuditWriteRequest> channel, CancellationToken ct)
    {
        await foreach (var req in channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                WriteOne(req);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failed);
                if (IsEnabled())
                    Console.Error.WriteLine($"[SecurityAuditWriter] write failed type={req.Event.Type} exType={ex.GetType().Name}");
            }
        }

        WriteMetaFilesIfAny();
    }

    private void WriteOne(AuditWriteRequest req)
    {
        var (dataReason, dataTarget, dataCaller, parseError, parseErrorReason) = TryExtractAuditFields(req.DataJson);
        var dataSha256 = ComputeSha256Hex(req.DataJson);

        // 2A: trusted fields come from the event envelope. Payload fields are recorded as claims.
        var eventTs = SecurityAuditFormat.SanitizeEventTimestamp(req.Event.Timestamp, req.WrittenAt);
        var claimReason = SecurityAuditFormat.ToClaimString(dataReason, fallback: "missing", maxChars: SecurityAuditFormat.MaxReasonChars);
        var claimTarget = SecurityAuditFormat.ToClaimString(dataTarget, fallback: "missing", maxChars: SecurityAuditFormat.MaxTargetChars);
        var eventSource = string.IsNullOrWhiteSpace(req.Event.Source) ? "unknown" : req.Event.Source.Trim();

        var dir = Path.GetDirectoryName(req.FullPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        lock (_fileGate)
        {
            var (writeFullPath, writeLogicalPath) = ResolveWritablePath(req.FullPath, req.LogicalPath, baseJsonOverheadBytes: 2048, payloadBytes: Encoding.UTF8.GetByteCount(req.DataJson));
            if (writeFullPath == null || writeLogicalPath == null)
            {
                Interlocked.Increment(ref _droppedBySize);
                _touchedAuditDirectories.Add(Path.GetDirectoryName(req.FullPath) ?? "");
                Console.Error.WriteLine($"[SecurityAuditWriter] security audit log reached max size; dropping writes path={req.LogicalPath} maxBytes={_maxFileBytes} eventType={req.Event.Type}");
                return;
            }

            for (var attempt = 0; attempt < 2; attempt++)
            {
                var prevHash = _lastEntryHashByLogicalPath.TryGetValue(writeLogicalPath, out var previousHash) ? previousHash : "";
                var material = new SecurityAuditFormat.AuditEntryMaterial(
                    ts: eventTs.ToString("o"),
                    action: req.Event.Type,
                    reason: claimReason,
                    target: claimTarget,
                    caller: eventSource,
                    event_id: req.Event.Id,
                    event_timestamp: eventTs.ToString("o"),
                    event_source: eventSource,
                    audit_writer: nameof(SecurityAuditWriter),
                    written_at: req.WrittenAt.ToString("o"),
                    prev_hash: prevHash,
                    reason_trust: "claim",
                    target_trust: "claim",
                    caller_trust: "event_source",
                    data_sha256: dataSha256,
                    data_bytes: Encoding.UTF8.GetByteCount(req.DataJson),
                    data_reason: dataReason,
                    data_target: dataTarget,
                    data_caller: dataCaller,
                    parse_error: parseError,
                    parse_error_reason: parseErrorReason
                );

                var materialJson = JsonSerializer.Serialize(material, JsonOptions);
                var entrySha256 = ComputeSha256Hex(materialJson);
                var final = new SecurityAuditFormat.AuditEntryFinal(
                    ts: material.ts,
                    action: material.action,
                    reason: material.reason,
                    target: material.target,
                    caller: material.caller,
                    event_id: material.event_id,
                    event_timestamp: material.event_timestamp,
                    event_source: material.event_source,
                    audit_writer: material.audit_writer,
                    written_at: material.written_at,
                    prev_hash: material.prev_hash,
                    reason_trust: material.reason_trust,
                    target_trust: material.target_trust,
                    caller_trust: material.caller_trust,
                    data_sha256: material.data_sha256,
                    data_bytes: material.data_bytes,
                    data_reason: material.data_reason,
                    data_target: material.data_target,
                    data_caller: material.data_caller,
                    parse_error: material.parse_error,
                    parse_error_reason: material.parse_error_reason,
                    entry_sha256: entrySha256
                );
                var jsonLine = JsonSerializer.Serialize(final, JsonOptions) + System.Environment.NewLine;
                var lineBytes = Encoding.UTF8.GetByteCount(jsonLine);

                if (CanAppendWithLimits(writeFullPath, lineBytes))
                {
                    File.AppendAllText(writeFullPath, jsonLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    _lastEntryHashByLogicalPath[writeLogicalPath] = entrySha256;
                    _touchedAuditDirectories.Add(Path.GetDirectoryName(writeFullPath) ?? "");
                    Interlocked.Increment(ref _written);
                    return;
                }

                var resolved = ResolveWritablePathExact(req.FullPath, req.LogicalPath, lineBytes);
                if (resolved.FullPath == null || resolved.LogicalPath == null)
                {
                    Interlocked.Increment(ref _droppedBySize);
                    _touchedAuditDirectories.Add(Path.GetDirectoryName(writeFullPath) ?? "");
                    Console.Error.WriteLine($"[SecurityAuditWriter] security audit log reached max size; dropping writes path={writeLogicalPath} maxBytes={_maxFileBytes} eventType={req.Event.Type}");
                    return;
                }

                writeFullPath = resolved.FullPath;
                writeLogicalPath = resolved.LogicalPath;
            }
        }
    }

    private (string? FullPath, string? LogicalPath) ResolveWritablePath(string baseFullPath, string baseLogicalPath, int baseJsonOverheadBytes, int payloadBytes)
    {
        var estimatedLineBytes = baseJsonOverheadBytes + payloadBytes;

        if (CanAppendWithLimits(baseFullPath, estimatedLineBytes))
            return (baseFullPath, baseLogicalPath);

        for (int i = 1; i <= 9; i++)
        {
            var full = BuildAuditFilePath(baseFullPath, i);
            var logical = BuildAuditLogicalPath(baseLogicalPath, i);
            if (CanAppendWithLimits(full, estimatedLineBytes))
                return (full, logical);
        }

        return (null, null);
    }

    private (string? FullPath, string? LogicalPath) ResolveWritablePathExact(string baseFullPath, string baseLogicalPath, int lineBytes)
    {
        if (CanAppendWithLimits(baseFullPath, lineBytes))
            return (baseFullPath, baseLogicalPath);

        for (int i = 1; i <= 9; i++)
        {
            var full = BuildAuditFilePath(baseFullPath, i);
            var logical = BuildAuditLogicalPath(baseLogicalPath, i);
            if (CanAppendWithLimits(full, lineBytes))
                return (full, logical);
        }

        return (null, null);
    }

    private bool CanAppendWithLimits(string fullPath, int lineBytes)
    {
        if (!CanAppend(fullPath, lineBytes))
            return false;

        var dir = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(dir))
            return false;

        try
        {
            if (!Directory.Exists(dir))
                return true;

            long totalBytes = 0;
            foreach (var file in Directory.EnumerateFiles(dir, "security-audit*.jsonl"))
            {
                try { totalBytes += new FileInfo(file).Length; }
                catch { }
            }

            return totalBytes + lineBytes < _maxTotalBytes;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildAuditFilePath(string baseFullPath, int i)
    {
        var dir = Path.GetDirectoryName(baseFullPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(baseFullPath);
        var ext = Path.GetExtension(baseFullPath);
        return Path.Combine(dir, $"{name}-{i}{ext}");
    }

    private static string BuildAuditLogicalPath(string baseLogicalPath, int i)
    {
        var idx = baseLogicalPath.LastIndexOf('/');
        var dir = idx >= 0 ? baseLogicalPath[..(idx + 1)] : "";
        var file = idx >= 0 ? baseLogicalPath[(idx + 1)..] : baseLogicalPath;
        var dot = file.LastIndexOf('.');
        if (dot <= 0)
            return $"{dir}{file}-{i}";
        var name = file[..dot];
        var ext = file[dot..];
        return $"{dir}{name}-{i}{ext}";
    }

    private bool CanAppend(string fullPath, int lineBytes)
    {
        var existing = new FileInfo(fullPath);
        if (!existing.Exists)
            return true;
        return existing.Length + lineBytes < _maxFileBytes;
    }

    private static string ComputeSha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? "");
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static (string DataReason, string DataTarget, string DataCaller, bool ParseError, string? ParseErrorReason) TryExtractAuditFields(string dataJson)
    {
        string dataReason = "";
        string dataTarget = "";
        string dataCaller = "";
        bool parseError = false;
        string? parseErrorReason = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(dataJson) && dataJson.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                using var doc = JsonDocument.Parse(dataJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetString(doc.RootElement, "reason", "Reason", out var reasonValue)) dataReason = reasonValue;
                    if (TryGetString(doc.RootElement, "target", "Target", out var targetValue)) dataTarget = targetValue;
                    if (TryGetString(doc.RootElement, "caller", "Caller", out var callerValue)) dataCaller = callerValue;
                }
            }
        }
        catch (Exception ex)
        {
            parseError = true;
            parseErrorReason = ex.GetType().Name;
        }

        return (dataReason, dataTarget, dataCaller, parseError, parseErrorReason);
    }

    private static bool TryGetString(JsonElement obj, string camel, string pascal, out string value)
    {
        if (obj.TryGetProperty(camel, out var v1) && v1.ValueKind == JsonValueKind.String)
        {
            value = v1.GetString() ?? "";
            return true;
        }

        if (obj.TryGetProperty(pascal, out var v2) && v2.ValueKind == JsonValueKind.String)
        {
            value = v2.GetString() ?? "";
            return true;
        }

        value = "";
        return false;
    }

    private sealed record AuditWriteRequest(DomainEvent Event, string DataJson, string LogicalPath, string FullPath, DateTime WrittenAt);

    private void WriteMetaFilesIfAny()
    {
        lock (_fileGate)
        {
            if (_touchedAuditDirectories.Count == 0)
                return;

            foreach (var dir in _touchedAuditDirectories)
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;

                try
                {
                    Directory.CreateDirectory(dir);
                    var metaPath = Path.Combine(dir, "security-audit.meta.json");
                    var meta = new
                    {
                        written_at = DateTime.UtcNow.ToString("o"),
                        audit_writer = nameof(SecurityAuditWriter),
                        max_file_bytes = _maxFileBytes,
                        max_total_bytes = _maxTotalBytes,
                        enqueued = Interlocked.Read(ref _enqueued),
                        written = Interlocked.Read(ref _written),
                        dropped = Interlocked.Read(ref _dropped),
                        failed = Interlocked.Read(ref _failed),
                        dropped_by_size = Interlocked.Read(ref _droppedBySize),
                        files_seen = _lastEntryHashByLogicalPath.Count,
                        last_entry_sha256_by_file = _lastEntryHashByLogicalPath,
                    };
                    File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, JsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                }
                catch (Exception ex)
                {
                    // Intentionally ignored: audit writer must never crash the game on shutdown.
                    Interlocked.Increment(ref _failed);
                    if (IsEnabled())
                        Console.Error.WriteLine($"[SecurityAuditWriter] meta write failed exType={ex.GetType().Name}");
                }
            }
        }
    }
}
