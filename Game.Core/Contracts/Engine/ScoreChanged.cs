namespace Game.Core.Contracts.Engine;

/// <summary>
/// Domain event: core.score.changed
/// Published when player score changes
/// </summary>
public sealed record ScoreChanged(
    int Score,
    int Added
)
{
    public const string EventType = "core.score.changed";
}
