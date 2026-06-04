using System;
using System.Collections.Generic;
using System.Linq;
using pkNX.Structures;

namespace pkNX.WinForms;

public static class ShopItemNameFormatter
{
    public static string[] MoveNames { get; set; } = [];
    public static Item8MachineTable MachineTable { get; set; } = Item8MachineTable.CreateDefault();

    public static string GetDisplayName(int item, bool includeID = false)
    {
        var itemName = GetItemName(item);
        var moveName = GetMachineMoveName(item);
        var display = moveName is null ? itemName : $"{itemName} - {moveName}";
        return includeID ? $"{display} ({item})" : display;
    }

    public static string GetSummary(IEnumerable<int> items, int maxItems = int.MaxValue)
    {
        var materialized = items as ICollection<int> ?? items.ToArray();
        var limit = Math.Max(1, maxItems);
        var summary = string.Join(", ", materialized.Take(limit).Select(z => GetDisplayName(z)));
        return materialized.Count > limit ? $"{summary}, ..." : summary;
    }

    public static string? GetMachineMoveName(int item)
    {
        if ((uint)item > ushort.MaxValue)
            return null;

        return MachineTable.TryGetMoveForItem((ushort)item, out var move) && (uint)move < (uint)MoveNames.Length
            ? MoveNames[move]
            : null;
    }

    private static string GetItemName(int item)
    {
        var names = ItemConverter.ItemNames;
        if ((uint)item >= (uint)names.Length)
            return item.ToString();

        var name = names[item];
        return string.IsNullOrWhiteSpace(name) ? $"Item {item}" : name;
    }

}
