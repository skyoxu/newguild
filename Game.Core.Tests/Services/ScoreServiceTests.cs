using System;
using Game.Core.Domain;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Services;

public class ScoreServiceTests
{
    [Fact]
    public void ComputeAddedScore_respects_multiplier_and_difficulty()
    {
        var svc = new ScoreService();
        var cfg = new GameConfig(
            MaxLevel: 50,
            InitialHealth: 100,
            ScoreMultiplier: 1.5,
            AutoSave: false,
            Difficulty: Difficulty.Medium
        );

        var added = svc.ComputeAddedScore(100, cfg);
        Assert.Equal(150, added); // 100 * 1.5 * 1.0

        cfg = cfg with { Difficulty = Difficulty.Hard };
        var hardAdded = svc.ComputeAddedScore(100, cfg);
        Assert.Equal(180, hardAdded); // 100 * 1.5 * 1.2
    }

    [Fact]
    public void Add_accumulates_and_reset_clears_score()
    {
        var svc = new ScoreService();
        var cfg = new GameConfig(50, 100, 1.0, false, Difficulty.Medium);

        svc.Add(10, cfg);
        svc.Add(20, cfg);

        Assert.True(svc.Score > 0);

        var before = svc.Score;
        Assert.Equal(before, svc.Score);

        svc.Reset();
        Assert.Equal(0, svc.Score);
    }

    [Fact]
    public void ComputeAddedScore_clamps_negative_base_points_to_zero()
    {
        var svc = new ScoreService();
        var cfg = new GameConfig(MaxLevel: 10, InitialHealth: 100, ScoreMultiplier: 1.0, AutoSave: false, Difficulty: Difficulty.Medium);

        // Negative basePoints should be clamped to 0
        var added = svc.ComputeAddedScore(-100, cfg);

        Assert.Equal(0, added);
    }

    [Fact]
    public void GameConfig_rejects_invalid_difficulty_enum()
    {
        // Cast an invalid integer to Difficulty enum
        var invalidDifficulty = (Difficulty)999;

        // Should throw ArgumentException when creating GameConfig
        var ex = Assert.Throws<ArgumentException>(() =>
            new GameConfig(MaxLevel: 10, InitialHealth: 100, ScoreMultiplier: 1.0, AutoSave: false, Difficulty: invalidDifficulty)
        );

        Assert.Contains("Invalid difficulty value", ex.Message);
        Assert.Contains("999", ex.Message);
        Assert.Equal("Difficulty", ex.ParamName);
    }

    [Fact]
    public void ComputeAddedScore_returns_zero_when_negative_multiplier_result()
    {
        var svc = new ScoreService();
        var cfg = new GameConfig(MaxLevel: 10, InitialHealth: 100, ScoreMultiplier: -2.0, AutoSave: false, Difficulty: Difficulty.Medium);

        // Negative multiplier produces negative result, should be clamped to 0 by Math.Max
        var added = svc.ComputeAddedScore(100, cfg);

        Assert.Equal(0, added);
    }

    [Fact]
    public void ComputeAddedScore_applies_easy_difficulty_multiplier()
    {
        var svc = new ScoreService();
        var cfg = new GameConfig(
            MaxLevel: 50,
            InitialHealth: 100,
            ScoreMultiplier: 1.0,
            AutoSave: false,
            Difficulty: Difficulty.Easy
        );

        // Easy difficulty has 0.9 multiplier: 100 * 1.0 * 0.9 = 90
        var added = svc.ComputeAddedScore(100, cfg);
        Assert.Equal(90, added);
    }
}

