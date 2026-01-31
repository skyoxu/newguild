using Game.Core.Domain.ValueObjects;
using Xunit;

namespace Game.Core.Tests.Domain.ValueObjects;

public class PositionTests
{
    [Fact]
    public void Add_Returns_New_Position_And_Keeps_Immutable()
    {
        var position = new Position(1, 2);
        var moved = position.Add(3, 4);
        Assert.Equal(1, position.X);
        Assert.Equal(2, position.Y);
        Assert.Equal(4, moved.X);
        Assert.Equal(6, moved.Y);
        Assert.NotEqual(position, moved);
    }
}

