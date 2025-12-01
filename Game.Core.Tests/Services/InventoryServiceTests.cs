using Game.Core.Domain;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Services;

public class InventoryServiceTests
{
    [Fact]
    public void Add_succeeds_when_within_max_slots()
    {
        var inv = new Inventory();
        var svc = new InventoryService(inv, maxSlots: 10);

        var added = svc.Add("item1", 5);

        Assert.Equal(5, added);
        Assert.Equal(1, svc.CountDistinct());
        Assert.Equal(5, svc.CountItem("item1"));
    }

    [Fact]
    public void Add_fails_when_max_slots_reached_for_new_item()
    {
        var inv = new Inventory();
        var svc = new InventoryService(inv, maxSlots: 2);

        // Fill up slots
        svc.Add("item1", 1);
        svc.Add("item2", 1);

        // Try to add a third distinct item - should fail
        var added = svc.Add("item3", 1);

        Assert.Equal(0, added);
        Assert.Equal(2, svc.CountDistinct());
    }

    [Fact]
    public void Add_succeeds_for_existing_item_even_when_max_slots_reached()
    {
        var inv = new Inventory();
        var svc = new InventoryService(inv, maxSlots: 2);

        // Fill up slots
        svc.Add("item1", 1);
        svc.Add("item2", 1);

        // Add more of existing item - should succeed
        var added = svc.Add("item1", 5);

        Assert.Equal(5, added);
        Assert.Equal(6, svc.CountItem("item1"));
    }

    [Fact]
    public void CountDistinct_returns_number_of_unique_items()
    {
        var inv = new Inventory();
        var svc = new InventoryService(inv);

        svc.Add("item1", 10);
        svc.Add("item2", 5);
        svc.Add("item3", 1);

        Assert.Equal(3, svc.CountDistinct());
    }

    [Fact]
    public void HasItem_returns_true_when_item_exists_with_required_count()
    {
        var inv = new Inventory();
        var svc = new InventoryService(inv);

        svc.Add("potion", 5);

        Assert.True(svc.HasItem("potion"));
        Assert.True(svc.HasItem("potion", atLeast: 3));
        Assert.False(svc.HasItem("potion", atLeast: 10));
    }

    [Fact]
    public void Remove_reduces_item_count()
    {
        var inv = new Inventory();
        var svc = new InventoryService(inv);

        svc.Add("sword", 10);
        var removed = svc.Remove("sword", 3);

        Assert.Equal(3, removed);
        Assert.Equal(7, svc.CountItem("sword"));
    }

    [Fact]
    public void Remove_returns_zero_when_item_not_found()
    {
        var inv = new Inventory();
        var svc = new InventoryService(inv);

        var removed = svc.Remove("nonexistent", 1);

        Assert.Equal(0, removed);
    }
}
