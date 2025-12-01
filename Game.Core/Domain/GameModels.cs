using Game.Core.Domain.ValueObjects;

namespace Game.Core.Domain;

public enum Difficulty
{
    Easy,
    Medium,
    Hard
}

public record GameConfig
{
    public int MaxLevel { get; init; }
    public int InitialHealth { get; init; }
    public double ScoreMultiplier { get; init; }
    public bool AutoSave { get; init; }
    public Difficulty Difficulty { get; init; }

    public GameConfig(
        int MaxLevel,
        int InitialHealth,
        double ScoreMultiplier,
        bool AutoSave,
        Difficulty Difficulty)
    {
        if (!Enum.IsDefined(typeof(Difficulty), Difficulty))
        {
            throw new ArgumentException(
                $"Invalid difficulty value: {Difficulty}. Must be one of: {string.Join(", ", Enum.GetNames(typeof(Difficulty)))}",
                nameof(Difficulty));
        }

        this.MaxLevel = MaxLevel;
        this.InitialHealth = InitialHealth;
        this.ScoreMultiplier = ScoreMultiplier;
        this.AutoSave = AutoSave;
        this.Difficulty = Difficulty;
    }
}

public record GameState(
    string Id,
    int Level,
    int Score,
    int Health,
    IReadOnlyList<string> Inventory,
    Position Position,
    DateTime Timestamp
);

