using Game.Core.Domain;
using Xunit;

namespace Game.Core.Tests.Domain;

public class PlayerTests
{
    [Fact]
    public void New_Player_Has_Full_Health_And_Origin_Position()
    {
        var player = new Player(maxHealth: 50);
        Assert.Equal(50, player.Health.Maximum);
        Assert.Equal(50, player.Health.Current);
        Assert.True(player.IsAlive);
        Assert.Equal(0, player.Position.X);
        Assert.Equal(0, player.Position.Y);
    }

    [Fact]
    public void Move_And_TakeDamage_Update_State()
    {
        var player = new Player(maxHealth: 10);
        player.Move(1.5, -2);
        Assert.Equal(1.5, player.Position.X);
        Assert.Equal(-2, player.Position.Y);
        player.TakeDamage(7);
        Assert.Equal(3, player.Health.Current);
    }
}

