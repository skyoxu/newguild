namespace Game.Core.Performance;

public sealed record PerformanceGateDecision(double P95Ms, double ThresholdMs)
{
    public bool IsOverBudget => P95Ms > ThresholdMs;
}

