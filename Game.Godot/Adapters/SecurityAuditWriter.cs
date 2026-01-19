using System;
using System.IO;
using System.Text;
using System.Text.Json;
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
    private readonly object _fileGate = new();
    private CancellationTokenSource? _cts;

    public SecurityAuditWriter(long? maxFileBytes = null)
    {
        _maxFileBytes = maxFileBytes ?? DefaultMaxFileBytes;
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
            _worker = Task.Run(() => RunAsync(_cts.Token));
        }
    }

    public async ValueTask DisposeAsync()
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

        if (cts != null)
            cts.Cancel();

        if (worker != null)
        {
            try { await worker; }
            catch { }
        }
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
            return false;

        return channel.Writer.TryWrite(req);
    }

    private static bool IsEnabled()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("GD_SECURE_MODE"), "1", StringComparison.Ordinal))
            return true;

        if (string.Equals(Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal))
            return true;

        var ci = Environment.GetEnvironmentVariable("CI");
        return string.Equals(ci, "1", StringComparison.Ordinal) ||
               string.Equals(ci, "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var channel = _channel;
        if (channel == null)
            return;

        await foreach (var req in channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                WriteOne(req);
            }
            catch (Exception ex)
            {
                if (IsEnabled())
                    Console.Error.WriteLine($"[SecurityAuditWriter] write failed type={req.Event.Type} exType={ex.GetType().Name}");
            }
        }
    }

    private void WriteOne(AuditWriteRequest req)
    {
        var (dataReason, dataTarget, dataCaller, parseError, parseErrorReason) = TryExtractAuditFields(req.DataJson);
        var dataSha256 = ComputeSha256Hex(req.DataJson);

        var auditEntry = new
        {
            ts = req.WrittenAt.ToString("o"),
            action = req.Event.Type,
            reason = string.IsNullOrWhiteSpace(dataReason) ? "event" : dataReason,
            target = string.IsNullOrWhiteSpace(dataTarget) ? req.Event.Source : dataTarget,
            caller = string.IsNullOrWhiteSpace(dataCaller) ? req.Event.Source : dataCaller,
            event_id = req.Event.Id,
            event_timestamp = req.Event.Timestamp.ToString("o"),
            event_source = req.Event.Source,
            audit_writer = nameof(EventBusAdapter),
            data_sha256 = dataSha256,
            data_bytes = Encoding.UTF8.GetByteCount(req.DataJson),
            data_reason = dataReason,
            data_target = dataTarget,
            data_caller = dataCaller,
            parse_error = parseError,
            parse_error_reason = parseErrorReason,
        };

        var jsonLine = JsonSerializer.Serialize(auditEntry, JsonOptions) + Environment.NewLine;
        var lineBytes = Encoding.UTF8.GetByteCount(jsonLine);

        var dir = Path.GetDirectoryName(req.FullPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        lock (_fileGate)
        {
            var (writeFullPath, writeLogicalPath) = ResolveWritablePath(req.FullPath, req.LogicalPath, lineBytes);
            if (writeFullPath == null || writeLogicalPath == null)
            {
                Console.Error.WriteLine($"[SecurityAuditWriter] security audit log reached max size; dropping writes path={req.LogicalPath} maxBytes={_maxFileBytes} eventType={req.Event.Type}");
                return;
            }

            File.AppendAllText(writeFullPath, jsonLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private (string? FullPath, string? LogicalPath) ResolveWritablePath(string baseFullPath, string baseLogicalPath, int lineBytes)
    {
        if (CanAppend(baseFullPath, lineBytes))
            return (baseFullPath, baseLogicalPath);

        for (int i = 1; i <= 9; i++)
        {
            var full = baseFullPath.Replace("security-audit.jsonl", $"security-audit-{i}.jsonl", StringComparison.OrdinalIgnoreCase);
            var logical = baseLogicalPath.Replace("security-audit.jsonl", $"security-audit-{i}.jsonl", StringComparison.OrdinalIgnoreCase);
            if (CanAppend(full, lineBytes))
                return (full, logical);
        }

        return (null, null);
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
                    if (TryGetString(doc.RootElement, "reason", "Reason", out var r)) dataReason = r;
                    if (TryGetString(doc.RootElement, "target", "Target", out var t)) dataTarget = t;
                    if (TryGetString(doc.RootElement, "caller", "Caller", out var c)) dataCaller = c;
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
}
