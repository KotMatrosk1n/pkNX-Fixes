using System;
using System.Collections.Generic;
using System.Linq;
using pkNX.Structures;

namespace pkNX.WinForms;

public static class ShopItemNameFormatter
{
    public static string[] MoveNames { get; set; } = [];

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

        return TryGetMachineMove((ushort)item, out var move) && (uint)move < (uint)MoveNames.Length
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

    private static bool TryGetMachineMove(ushort item, out ushort move)
    {
        if (item == 1230)
        {
            move = Legal.TMHM_SWSH[0];
            return true;
        }

        var tmIndex = Array.IndexOf(Legal.Pouch_TM_SWSH, item);
        if (tmIndex >= 0 && tmIndex + 1 < Legal.TMHM_SWSH.Length)
        {
            move = Legal.TMHM_SWSH[tmIndex + 1];
            return true;
        }

        var trIndex = Array.IndexOf(Legal.Pouch_TR_SWSH, item);
        if (trIndex >= 0 && trIndex < Legal.TR_SWSH.Length)
        {
            move = Legal.TR_SWSH[trIndex];
            return true;
        }

        move = 0;
        return false;
    }
}
