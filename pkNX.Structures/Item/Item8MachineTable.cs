using System;
using System.Collections.Generic;
using System.Linq;
using static System.Buffers.Binary.BinaryPrimitives;

namespace pkNX.Structures;

public sealed class Item8MachineTable
{
    public const int TMCount = 100;
    public const int TRCount = 100;
    public const int Count = TMCount + TRCount;

    private const int TRMachineSlotStart = TMCount;
    private const int TableHeaderPadding = 0x44;
    private const int EntrySize = 4;
    private const int MoveOffset = 2;

    private readonly ushort[] Moves;
    private readonly Move[] MoveChoices;
    private readonly int TableOffset;

    private Item8MachineTable(ushort[] moves, Move[] moveChoices, int tableOffset)
    {
        Moves = moves;
        MoveChoices = moveChoices;
        TableOffset = tableOffset;
    }

    public static Item8MachineTable FromItemData(ReadOnlySpan<byte> data, IReadOnlyList<ushort>? allowedMoves = null)
    {
        var moves = CreateDefaultMoves();
        var offset = TryGetTableOffset(data, out var tableOffset) ? tableOffset : -1;
        if (offset >= 0)
        {
            for (int i = 0; i < Count; i++)
                moves[i] = ReadUInt16LittleEndian(data[(offset + (i * EntrySize) + MoveOffset)..]);
        }

        return new Item8MachineTable(moves, CreateMoveChoices(allowedMoves), offset);
    }

    public static Item8MachineTable CreateDefault(IReadOnlyList<ushort>? allowedMoves = null) =>
        new(CreateDefaultMoves(), CreateMoveChoices(allowedMoves), -1);

    public ushort GetMove(int slot)
    {
        return (uint)slot < Count ? Moves[slot] : (ushort)0;
    }

    public void SetMove(int slot, ushort move)
    {
        if ((uint)slot >= Count)
            return;

        Moves[slot] = move;
    }

    public Move[] GetMoveChoices() => MoveChoices;

    public void WriteTo(Span<byte> data)
    {
        if (TableOffset < 0 || TableOffset + (Count * EntrySize) > data.Length)
            return;

        for (int i = 0; i < Count; i++)
            WriteUInt16LittleEndian(data[(TableOffset + (i * EntrySize) + MoveOffset)..], Moves[i]);
    }

    public bool TryGetMoveForItem(ushort item, out ushort move)
    {
        if (TryGetMachineSlotForItem(item, out var slot))
        {
            move = GetMove(slot);
            return move != 0;
        }

        move = 0;
        return false;
    }

    public static bool TryGetMachineSlotForItem(ushort item, out int slot)
    {
        if (item == 1230) // TM00 is stored before TM01 in the machine table.
        {
            slot = 0;
            return true;
        }

        var tmIndex = Array.IndexOf(Legal.Pouch_TM_SWSH, item);
        if (tmIndex >= 0 && tmIndex + 1 < TMCount)
        {
            slot = tmIndex + 1;
            return true;
        }

        var trIndex = Array.IndexOf(Legal.Pouch_TR_SWSH, item);
        if (trIndex >= 0 && trIndex < TRCount)
        {
            slot = TRMachineSlotStart + trIndex;
            return true;
        }

        slot = 0;
        return false;
    }

    private static bool TryGetTableOffset(ReadOnlySpan<byte> data, out int offset)
    {
        offset = -1;
        if (data.Length < 4)
            return false;

        var tableBase = ReadUInt16LittleEndian(data[2..]) * 2;
        var tableOffset = tableBase + TableHeaderPadding;
        if (tableOffset < 0 || tableOffset + (Count * EntrySize) > data.Length)
            return false;

        offset = tableOffset;
        return true;
    }

    private static ushort[] CreateDefaultMoves()
    {
        var moves = new ushort[Count];
        Legal.TMHM_SWSH.CopyTo(moves, 0);
        Legal.TR_SWSH.CopyTo(moves, TRMachineSlotStart);
        return moves;
    }

    private static Move[] CreateMoveChoices(IReadOnlyList<ushort>? allowedMoves)
    {
        var source = allowedMoves is { Count: > 0 }
            ? allowedMoves
            : Legal.TMHM_SWSH.Concat(Legal.TR_SWSH).ToArray();

        var seen = new HashSet<Move>();
        var result = new List<Move> { Move.None };
        seen.Add(Move.None);

        foreach (var move in source)
        {
            var value = (Move)move;
            if (seen.Add(value))
                result.Add(value);
        }

        return result.ToArray();
    }
}
