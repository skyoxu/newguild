using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Game.Core.Ports;

namespace Game.Godot.Adapters.Db;

public partial class DbPerfProbe : Node
{
    public global::Godot.Collections.Dictionary RunAndWriteSummary(string dbPath, string outDirAbsolute)
    {
        var querySamples = ParseIntEnv("PERF_DB_QUERY_SAMPLES", 120, min: 10, max: 10_000);
        var largeRows = ParseIntEnv("PERF_DB_LARGE_ROWS", 10_000, min: 100, max: 500_000);
        var largeRuns = ParseIntEnv("PERF_DB_LARGE_RUNS", 5, min: 1, max: 50);
        var concurrency = ParseIntEnv("PERF_DB_CONCURRENCY", 8, min: 1, max: 64);
        var leakIterations = ParseIntEnv("PERF_DB_LEAK_ITERATIONS", 500, min: 10, max: 200_000);

        if (string.IsNullOrWhiteSpace(outDirAbsolute))
            throw new ArgumentException("outDirAbsolute cannot be empty", nameof(outDirAbsolute));

        Directory.CreateDirectory(outDirAbsolute);

        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
            ["db_path"] = dbPath,
            ["parameters"] = new Dictionary<string, object?>
            {
                ["query_samples"] = querySamples,
                ["large_rows"] = largeRows,
                ["large_runs"] = largeRuns,
                ["concurrency"] = concurrency,
                ["leak_iterations"] = leakIterations,
            },
        };

        var absDbPath = ProjectSettings.GlobalizePath(dbPath);
        TryDeleteFile(absDbPath);

        // DB_STARTUP_IMPACT / DB_CONNECTION_TIME
        var firstOpenMs = MeasureMs(() =>
        {
            using var db = OpenDb(dbPath);
        });

        var reopenMs = MeasureMs(() =>
        {
            using var db = OpenDb(dbPath);
        });

        payload["DB_STARTUP_IMPACT"] = new Dictionary<string, object?>
        {
            ["first_open_ms"] = firstOpenMs,
        };

        payload["DB_CONNECTION_TIME"] = new Dictionary<string, object?>
        {
            ["first_open_ms"] = firstOpenMs,
            ["reopen_ms"] = reopenMs,
        };

        // Prepare schema for query/large-result/concurrency tests
        using (var db = OpenDb(dbPath))
        {
            db.Db.ExecuteNonQueryAsync(SqlStatement.NoParameters("CREATE TABLE IF NOT EXISTS perf_kv (k INTEGER PRIMARY KEY, v INTEGER);")).GetAwaiter().GetResult();
            db.Db.ExecuteNonQueryAsync(SqlStatement.NoParameters("DELETE FROM perf_kv;")).GetAwaiter().GetResult();
            db.Db.ExecuteNonQueryAsync(SqlStatement.NoParameters("CREATE TABLE IF NOT EXISTS perf_big (id INTEGER PRIMARY KEY, v INTEGER);")).GetAwaiter().GetResult();
            db.Db.ExecuteNonQueryAsync(SqlStatement.NoParameters("DELETE FROM perf_big;")).GetAwaiter().GetResult();
        }

        // DB_QUERY_P95 (scalar query)
        var queryTimes = new List<double>(capacity: querySamples);
        using (var db = OpenDb(dbPath))
        {
            var stmt = SqlStatement.NoParameters("SELECT 1;");
            for (var i = 0; i < querySamples; i++)
            {
                var ms = MeasureMs(() => { db.Db.ExecuteScalarAsync(stmt).GetAwaiter().GetResult(); });
                queryTimes.Add(ms);
            }
        }
        var qStats = Percentiles(queryTimes);
        payload["DB_QUERY_P95"] = new Dictionary<string, object?>
        {
            ["samples"] = querySamples,
            ["p50_ms"] = qStats.P50,
            ["p95_ms"] = qStats.P95,
            ["mean_ms"] = qStats.Mean,
            ["max_ms"] = qStats.Max,
        };

        // DB_LARGE_RESULT
        using (var db = OpenDb(dbPath))
        {
            db.Db.ExecuteNonQueryAsync(SqlStatement.NoParameters("BEGIN TRANSACTION;")).GetAwaiter().GetResult();
            // Build per-iteration to avoid mutating dictionaries in place.
            for (var i = 0; i < largeRows; i++)
            {
                var stmt = SqlStatement.WithParameters(
                    "INSERT INTO perf_big(v) VALUES(@V);",
                    new Dictionary<string, object?> { ["@V"] = i });
                db.Db.ExecuteNonQueryAsync(stmt).GetAwaiter().GetResult();
            }
            db.Db.ExecuteNonQueryAsync(SqlStatement.NoParameters("COMMIT;")).GetAwaiter().GetResult();
        }

        var largeTimes = new List<double>(capacity: largeRuns);
        var largeRowCounts = new List<int>(capacity: largeRuns);
        for (var i = 0; i < largeRuns; i++)
        {
            using var db = OpenDb(dbPath);
            var ms = MeasureMs(() =>
            {
                var rows = db.Db.QueryAsync(SqlStatement.NoParameters("SELECT v FROM perf_big ORDER BY id;")).GetAwaiter().GetResult();
                largeRowCounts.Add(rows.Count);
            });
            largeTimes.Add(ms);
        }
        var lStats = Percentiles(largeTimes);
        payload["DB_LARGE_RESULT"] = new Dictionary<string, object?>
        {
            ["rows"] = largeRowCounts.Count > 0 ? largeRowCounts.Max() : 0,
            ["runs"] = largeRuns,
            ["p50_ms"] = lStats.P50,
            ["p95_ms"] = lStats.P95,
            ["mean_ms"] = lStats.Mean,
            ["max_ms"] = lStats.Max,
        };

        // DB_CONCURRENCY: open + scalar query per worker
        var concurrentTimes = new List<double>(capacity: concurrency);
        var tasks = new List<Task>(capacity: concurrency);
        var lockObj = new object();
        for (var i = 0; i < concurrency; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var ms = MeasureMs(() =>
                {
                    using var db = OpenDb(dbPath);
                    db.Db.ExecuteScalarAsync(SqlStatement.NoParameters("SELECT 1;")).GetAwaiter().GetResult();
                });
                lock (lockObj) { concurrentTimes.Add(ms); }
            }));
        }
        Task.WaitAll(tasks.ToArray());
        var cStats = Percentiles(concurrentTimes);
        payload["DB_CONCURRENCY"] = new Dictionary<string, object?>
        {
            ["connections"] = concurrency,
            ["p50_ms"] = cStats.P50,
            ["p95_ms"] = cStats.P95,
            ["mean_ms"] = cStats.Mean,
            ["max_ms"] = cStats.Max,
        };

        // DB_MEMORY_LEAK (best-effort): repeated open/query/close and GC heap deltas
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var startBytes = GC.GetTotalMemory(forceFullCollection: true);
        var startMs = Stopwatch.GetTimestamp();
        for (var i = 0; i < leakIterations; i++)
        {
            using var db = OpenDb(dbPath);
            db.Db.ExecuteScalarAsync(SqlStatement.NoParameters("SELECT 1;")).GetAwaiter().GetResult();
        }
        var elapsedMs = ElapsedMs(startMs);
        var endBytes = GC.GetTotalMemory(forceFullCollection: true);
        payload["DB_MEMORY_LEAK"] = new Dictionary<string, object?>
        {
            ["iterations"] = leakIterations,
            ["duration_ms"] = elapsedMs,
            ["start_bytes"] = startBytes,
            ["end_bytes"] = endBytes,
            ["delta_bytes"] = endBytes - startBytes,
        };

        var outPath = Path.Combine(outDirAbsolute, "db-perf-summary.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) + System.Environment.NewLine, new UTF8Encoding(false));

        return new global::Godot.Collections.Dictionary
        {
            ["out_path"] = outPath,
            ["db_path"] = dbPath,
            ["ok"] = true,
        };
    }

    private sealed class DbHolder : IDisposable
    {
        private readonly GodotSQLiteDatabase _db;
        public GodotSQLiteDatabase Db => _db;
        public DbHolder(GodotSQLiteDatabase db) => _db = db;
        public void Dispose()
        {
            try { _db.CloseAsync().Wait(); } catch { }
        }
    }

    private static DbHolder OpenDb(string dbPath)
    {
        var db = new GodotSQLiteDatabase(dbPath);
        db.OpenAsync().Wait();
        return new DbHolder(db);
    }

    private static double MeasureMs(Action action)
    {
        var start = Stopwatch.GetTimestamp();
        action();
        return ElapsedMs(start);
    }

    private static double ElapsedMs(long startTimestamp)
    {
        var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        return elapsed * 1000.0 / Stopwatch.Frequency;
    }

    private static (double P50, double P95, double Mean, double Max) Percentiles(List<double> samples)
    {
        if (samples.Count == 0) return (0, 0, 0, 0);
        var sorted = samples.OrderBy(x => x).ToArray();
        var p50 = sorted[(int)((sorted.Length - 1) * 0.50)];
        var p95 = sorted[(int)((sorted.Length - 1) * 0.95)];
        var mean = samples.Average();
        var max = sorted[sorted.Length - 1];
        return (p50, p95, mean, max);
    }

    private static int ParseIntEnv(string name, int def, int min, int max)
    {
        try
        {
            var s = System.Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out var v))
            {
                if (v < min) return min;
                if (v > max) return max;
                return v;
            }
        }
        catch { }
        return def;
    }

    private static void TryDeleteFile(string absPath)
    {
        try
        {
            if (File.Exists(absPath))
                File.Delete(absPath);
        }
        catch { }
    }
}
