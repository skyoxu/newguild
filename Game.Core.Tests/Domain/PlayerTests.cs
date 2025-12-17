using Game.Core.Domain;
using Xunit;

namespace Game.Core.Tests.Domain;

public class PlayerTests
{
    [Fact]
    public void New_Player_Has_Full_Health_And_Origin_Position()
    {
        var p = new Player(maxHealth: 50);
        Assert.Equal(50, p.Health.Maximum);
        Assert.Equal(50, p.Health.Current);
        Assert.True(p.IsAlive);
        Assert.Equal(0, p.Position.X);
        Assert.Equal(0, p.Position.Y);
    }

    [Fact]
    public void Move_And_TakeDamage_Update_State()
    {
        var p = new Player(maxHealth: 10);
        p.Move(1.5, -2);
        Assert.Equal(1.5, p.Position.X);
        Assert.Equal(-2, p.Position.Y);
        p.TakeDamage(7);
        Assert.Equal(3, p.Health.Current);
    }
}

