namespace Game.Core.Performance;

public static class PerformanceMetrics
{
    public const string StartupTimeUs = "startup_time_us";
    public const string MenuFrameTimeUs = "menu_frame_time_us";
    public const string GameFrameTimeUs = "game_frame_time_us";
    public const string DbQueryTimeUs = "db_query_time_us";
    public const string SignalLatencyUs = "signal_latency_us";
    public const string MemoryPeakBytes = "memory_peak_bytes";
    public const string GcPauseTimeUs = "gc_pause_us";
    public const string CustomMetricPrefix = "custom_";
}
