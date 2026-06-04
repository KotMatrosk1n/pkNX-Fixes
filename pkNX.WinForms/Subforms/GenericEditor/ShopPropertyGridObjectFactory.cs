using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using pkNX.Structures.FlatBuffers.LGPE;
using SWSH = pkNX.Structures.FlatBuffers.SWSH;

namespace pkNX.WinForms;

public static class ShopPropertyGridObjectFactory
{
    public static object Create(object value) => value switch
    {
        SingleShop shop => new SingleShopPropertyGridObject(CreateInventory(shop.Inventories)),
        MultiShop shop => new MultiShopPropertyGridObject(shop.Inventories.Select(CreateInventory).ToList()),
        SWSH.SingleShop shop => new SingleShopPropertyGridObject(CreateInventory(shop.Inventories)),
        SWSH.MultiShop shop => new MultiShopPropertyGridObject(shop.Inventories.Select(CreateInventory).ToList()),
        _ => value,
    };

    private static ShopInventoryPropertyGridObject CreateInventory(Inventory inventory)
        => new(() => inventory.Items, value => inventory.Items = value);

    private static ShopInventoryPropertyGridObject CreateInventory(SWSH.Inventory inventory)
        => new(() => inventory.Items, value => inventory.Items = value);
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class SingleShopPropertyGridObject(ShopInventoryPropertyGridObject inventory)
{
    public ShopInventoryPropertyGridObject Inventories { get; } = inventory;
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class MultiShopPropertyGridObject(IList<ShopInventoryPropertyGridObject> inventories)
{
    public IList<ShopInventoryPropertyGridObject> Inventories { get; } = inventories;
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class ShopInventoryPropertyGridObject(Func<IList<int>> getItems, Action<IList<int>> setItems)
{
    [Editor(typeof(ShopItemListUITypeEditor), typeof(UITypeEditor))]
    public IList<int> Items
    {
        get => getItems();
        set => setItems(value);
    }

    public override string ToString() => ShopItemNameFormatter.GetSummary(Items);
}
