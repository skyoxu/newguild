using System;
using Game.Core.Domain.ValueObjects;
using Xunit;

namespace Game.Core.Tests.Domain.ValueObjects;

public class HealthTests
{
    [Fact]
    public void Constructor_Sets_Current_Equals_Max_And_Disallows_Negative()
    {
        var health = new Health(100);
        Assert.Equal(100, health.Maximum);
        Assert.Equal(100, health.Current);
        Assert.True(health.IsAlive);
    }

    [Fact]
    public void TakeDamage_Clamps_At_Zero_And_Is_Immutable()
    {
        var initial = new Health(10);
        var afterDamage = initial.TakeDamage(3);
        Assert.Equal(10, initial.Current);
        Assert.Equal(7, afterDamage.Current);

        var dead = afterDamage.TakeDamage(100);
        Assert.Equal(0, dead.Current);
        Assert.False(dead.IsAlive);
    }

    [Fact]
    public void TakeDamage_Negative_Throws()
    {
        var health = new Health(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => health.TakeDamage(-1));
    }
}
