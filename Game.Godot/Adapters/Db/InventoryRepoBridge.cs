using System.Linq;
using Godot;
using Game.Core.Ports;

namespace Game.Godot.Adapters.Db;

public partial class InventoryRepoBridge : Node
{
    private ISqlDatabase GetDb() => GetNode<SqliteDataStore>("/root/SqlDb");

    public bool Add(string itemId, int qty)
    {
        var repo = new SqlInventoryRepository(GetDb());
        repo.AddAsync(itemId, qty).GetAwaiter().GetResult();
        return true;
    }

    public string[] All()
    {
        var repo = new SqlInventoryRepository(GetDb());
        var list = repo.AllAsync().GetAwaiter().GetResult();
        return list.Select(x => $"{x.ItemId}:{x.Qty}").ToArray();
    }
}
