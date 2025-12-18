using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Repositories;
using Xunit;

namespace Game.Core.Tests.Repositories;

public class InMemoryInventoryRepositoryTests
{
    [Fact]
    public async Task Add_Get_All_Flow()
    {
        var repo = new InMemoryInventoryRepository();
        var i1 = await repo.AddAsync("iron", 3);
        Assert.Equal(3, i1.Qty);
        var i2 = await repo.AddAsync("iron", 2);
        Assert.Equal(5, i2.Qty);

        var one = await repo.GetAsync("iron");
        Assert.NotNull(one);
        Assert.Equal(5, one!.Qty);

        var all = await repo.AllAsync();
        all.Should().ContainSingle(x => x.ItemId == "iron" && x.Qty == 5);
    }

    [Fact]
    public async Task GetAsync_WithNonExistentItem_ReturnsNull()
    {
        // Arrange
        var repo = new InMemoryInventoryRepository();

        // Act - get item that was never added
        var result = await repo.GetAsync("nonexistent-item");

        // Assert - should return null (tests line 19 false branch)
        result.Should().BeNull();
    }
}
