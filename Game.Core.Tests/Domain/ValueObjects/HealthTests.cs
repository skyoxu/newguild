using System;
using Game.Core.Domain.ValueObjects;
using Xunit;

namespace Game.Core.Tests.Domain.ValueObjects;

public class HealthTests
{
    [Fact]
    public void Constructor_Sets_Current_Equals_Max_And_Disallows_Negative()
    {
        var h = new Health(100);
        Assert.Equal(100, h.Maximum);
        Assert.Equal(100, h.Current);
        Assert.True(h.IsAlive);
    }

    [Fact]
    public void TakeDamage_Clamps_At_Zero_And_Is_Immutable()
    {
        var h = new Health(10);
        var h2 = h.TakeDamage(3);
        Assert.Equal(10, h.Current);
        Assert.Equal(7, h2.Current);

        var h3 = h2.TakeDamage(100);
        Assert.Equal(0, h3.Current);
        Assert.False(h3.IsAlive);
    }

    [Fact]
    public void TakeDamage_Negative_Throws()
    {
        var h = new Health(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => h.TakeDamage(-1));
    }
}
