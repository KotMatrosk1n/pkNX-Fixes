using System;
using System.Collections.Generic;
using System.ComponentModel;
using static System.Buffers.Binary.BinaryPrimitives;

namespace pkNX.Structures;

public class Item8(int id, Memory<byte> Raw, Item8MachineTable? machineTable = null)
{
    private const string Battle = "Battle";
    private const string Field = "Field Use";
    private const string Inventory = "Inventory";
    private const string Machine = "Machine";
    private const string Mart = "Mart";
    private const string Pokemon = "Pokemon Effects";
    private const string RawCategory = "Raw / Unknown";
    private const int TRMachineSlotStart = 100;
    private const int LastMachineSlot = TRMachineSlotStart + 99;
    private readonly Item8MachineTable MachineTable = machineTable ?? Item8MachineTable.CreateDefault();

    [Browsable(false)]
    public Span<byte> Data => Raw.Span;

    private const int SIZE = 0x30;
    public readonly int ItemID = id;

    [Category(Mart), Description("Purchase price in Poke Dollars.")]
    public uint Price { get => ReadUInt32LittleEndian(Data); set => WriteUInt32LittleEndian(Data, value); }

    [Category(Mart), DisplayName("Watts Price"), Description("Purchase price in Watts, used by Watt traders and related shops.")]
    public uint PriceWatts { get => ReadUInt16LittleEndian(Data[0x04..]); set => WriteUInt32LittleEndian(Data[0x04..], value); }

    [Category(Mart), DisplayName("Alternate Price"), Description("Secondary currency price, such as BP or Dynite Ore.")]
    public uint PriceAlternate // BP, Dynite Ore
    {
        get => ReadUInt16LittleEndian(Data[(0x08)..]);
        set => WriteUInt32LittleEndian(Data[(0x08)..], value);
    }

    [Category(Inventory), Description("Bag pocket where the item appears.")]
    public PouchID Pouch
    {
        get => (PouchID)(Data[0x11] & 0xF);
        set => Data[0x11] = (byte)((Data[0x11] & 0xF0) | ((byte)value & 0xF));
    }

    [Category(RawCategory), DisplayName("Pouch Flags"), Description("Upper nibble packed with the pouch value. Purpose is not fully decoded.")]
    public byte PouchFlags
    {
        get => (byte)(Data[0x11] >> 4);
        set => Data[0x11] = (byte)((Data[0x11] & 0x0F) | ((value & 0xF) << 4));
    }

    [Category(Battle), DisplayName("Fling Power"), Description("Power used when this item is thrown with Fling.")]
    public byte FlingPower
    {
        get => Data[0x12];
        set => Data[0x12] = value;
    }

    [Category(Field), DisplayName("Field Use Type"), Description("General field-use behavior, such as medicine, TM, spray, evolution item, or berry.")]
    public FieldItemType EffectField
    {
        get => (FieldItemType)Data[0x13];
        set => Data[0x13] = (byte)value;
    }

    [Category(Field), DisplayName("Can Use on Pokemon"), Description("Allows this item to be used on a Pokemon from the party/menu.")]
    public bool CanUseOnPokemon
    {
        get => Data[0x15] == 1;
        set => Data[0x15] = (byte)(value ? 1 : 0);
    }

    [Category(RawCategory), DisplayName("Field Flags"), Description("Unknown field-use flags.")]
    public byte FieldFlags
    {
        get => Data[0x14];
        set => Data[0x14] = value;
    }

    [Category(RawCategory), DisplayName("Item Type"), Description("Unknown item type/classification byte.")]
    public byte ItemType
    {
        get => Data[0x16];
        set => Data[0x16] = value;
    }

    [Category(RawCategory), DisplayName("Sort Index"), Description("Unknown sort/order byte used by the bag or item table. For TMs/TRs, this usually matches the machine slot.")]
    public byte SortIndex
    {
        get => Data[0x18];
        set => Data[0x18] = value;
    }

    [Category(Inventory), DisplayName("Item Sprite"), Description("Icon/sprite index used by the bag UI.")]
    public int ItemSprite
    {
        get => ReadInt16LittleEndian(Data[0x1A..]);
        set => WriteInt16LittleEndian(Data[0x1A..], (short)value);
    }

    [Category(Inventory), DisplayName("Group Type"), Description("Item group used for related item families, such as Balls, Berries, TMs, or Gems.")]
    public GroupIndexType GroupType
    {
        get => (GroupIndexType)Data[0x1C];
        set => Data[0x1C] = (byte)value;
    }

    [Category(Inventory), DisplayName("Group Index"), Description("Index inside the selected group type.")]
    public byte GroupIndex
    {
        get => Data[0x1D];
        set => Data[0x1D] = value;
    }

    [Category(Machine), DisplayName("Is TM/TR"), Description("True when this item points at the Sword/Shield TM/TR machine table.")]
    public bool IsTechnicalMachine => GroupType == GroupIndexType.TM && EffectField == FieldItemType.TM && GroupIndex <= LastMachineSlot;

    [Category(Machine), DisplayName("Machine Type"), Description("TM/TR side of the Sword/Shield machine table. Changing this keeps the same machine number when possible."), RefreshProperties(RefreshProperties.All)]
    public MachineKind TechnicalMachineType
    {
        get
        {
            if (!IsTechnicalMachine)
                return MachineKind.None;

            return GroupIndex >= TRMachineSlotStart ? MachineKind.TR : MachineKind.TM;
        }
        set
        {
            if (value == MachineKind.None)
                return;

            var number = TechnicalMachineNumber;
            var max = value == MachineKind.TR ? Legal.TR_SWSH.Length - 1 : Legal.TMHM_SWSH.Length - 1;
            number = Math.Clamp(number, 0, max);
            SetMachineSlot(value == MachineKind.TR ? TRMachineSlotStart + number : number);
        }
    }

    [Category(Machine), DisplayName("Machine Number"), Description("TM/TR number inside the current machine type. TM00 is 0; TR00 is 0."), RefreshProperties(RefreshProperties.All)]
    public int TechnicalMachineNumber
    {
        get => TechnicalMachineType == MachineKind.TR ? GroupIndex - TRMachineSlotStart : GroupIndex;
        set
        {
            var kind = TechnicalMachineType == MachineKind.TR ? MachineKind.TR : MachineKind.TM;
            var max = kind == MachineKind.TR ? Legal.TR_SWSH.Length - 1 : Legal.TMHM_SWSH.Length - 1;
            var number = Math.Clamp(value, 0, max);
            SetMachineSlot(kind == MachineKind.TR ? TRMachineSlotStart + number : number);
        }
    }

    [Category(Machine), DisplayName("Machine Slot"), Description("Raw move table slot used by the item. 0-99 = TMs, 100-199 = TRs."), RefreshProperties(RefreshProperties.All)]
    public int TechnicalMachineSlot
    {
        get => GroupIndex;
        set => SetMachineSlot(Math.Clamp(value, 0, LastMachineSlot));
    }

    [Category(Machine), DisplayName("Teaches Move"), Description("Move taught by this TM/TR item. Changing this writes the current Sword/Shield machine slot."), TypeConverter(typeof(MachineMoveConverter)), RefreshProperties(RefreshProperties.All)]
    public Move TechnicalMachineMove
    {
        get => IsTechnicalMachine ? (Move)MachineTable.GetMove(GroupIndex) : Move.None;
        set
        {
            if (value == Move.None || !IsTechnicalMachine)
                return;

            MachineTable.SetMove(GroupIndex, (ushort)value);
        }
    }

    [Category(Field), DisplayName("Cures Status"), Description("Battle status conditions cured by this item.")]
    public BattleStatusFlags CureStatus
    {
        get => (BattleStatusFlags)Data[0x1E];
        set => Data[0x1E] = (byte)value;
    }

    [Browsable(false)]
    public byte Boost0
    {
        get => Data[0x1F];
        set => Data[0x1F] = value;
    }

    [Browsable(false)]
    public byte Boost1
    {
        get => Data[0x20];
        set => Data[0x20] = value;
    }

    [Browsable(false)]
    public byte Boost2
    {
        get => Data[0x21];
        set => Data[0x21] = value;
    }

    [Browsable(false)]
    public byte Boost3
    {
        get => Data[0x22];
        set => Data[0x22] = value;
    }

    [Category(Field), DisplayName("Can Target Fainted Pokemon"), Description("Allows this item to target a fainted Pokemon, used by Revive-style items and Rare Candy.")]
    public bool CanTargetFaintedPokemon
    {
        get => ((Boost0 >> 0) & 1) == 1;
        set => Boost0 = (byte)((Boost0 & ~(1 << 0)) | ((value ? 1 : 0) << 0));
    }

    [Category(Field), DisplayName("Revives Whole Party"), Description("Revive-style flag used for items that affect the whole party.")]
    public bool ReviveAll
    {
        get => ((Boost0 >> 1) & 1) == 1;
        set => Boost0 = (byte)((Boost0 & ~(1 << 1)) | ((value ? 1 : 0) << 1));
    }

    [Category(Field), DisplayName("Level Up"), Description("Makes the item trigger a level-up effect, used by Rare Candy.")]
    public bool LevelUp
    {
        get => ((Boost0 >> 2) & 1) == 1;
        set => Boost0 = (byte)((Boost0 & ~(1 << 2)) | ((value ? 1 : 0) << 2));
    }

    [Category(Field), DisplayName("Evolution Stone"), Description("Makes the item behave as an evolution item.")]
    public bool EvoStone
    {
        get => ((Boost0 >> 3) & 1) == 1;
        set => Boost0 = (byte)((Boost0 & ~(1 << 3)) | ((value ? 1 : 0) << 3));
    }

    [Category(Battle), DisplayName("Attack Boost"), Description("Battle stat stage boost applied to Attack.")]
    public int BoostATK { get => Boost0 >> 4; set => Boost0 = (byte)((Boost0 & 0xF) | ((value & 0xF) << 4)); }

    [Category(Battle), DisplayName("Defense Boost"), Description("Battle stat stage boost applied to Defense.")]
    public int BoostDEF { get => Boost1 & 0xF; set => Boost1 = (byte)((Boost1 & ~0xF) | (value & 0xF)); }

    [Category(Battle), DisplayName("Sp. Atk Boost"), Description("Battle stat stage boost applied to Sp. Atk.")]
    public int BoostSPA { get => Boost1 >> 4; set => Boost1 = (byte)((Boost1 & 0xF) | ((value & 0xF) << 4)); }

    [Category(Battle), DisplayName("Sp. Def Boost"), Description("Battle stat stage boost applied to Sp. Def.")]
    public int BoostSPD { get => Boost2 & 0xF; set => Boost2 = (byte)((Boost2 & ~0xF) | (value & 0xF)); }

    [Category(Battle), DisplayName("Speed Boost"), Description("Battle stat stage boost applied to Speed.")]
    public int BoostSPE { get => Boost2 >> 4; set => Boost2 = (byte)((Boost2 & 0xF) | ((value & 0xF) << 4)); }

    [Category(Battle), DisplayName("Accuracy Boost"), Description("Battle stat stage boost applied to Accuracy.")]
    public int BoostACC { get => Boost3 & 0xF; set => Boost3 = (byte)((Boost3 & ~0xF) | (value & 0xF)); }

    [Category(Battle), DisplayName("Critical Hit Boost"), Description("Battle critical-hit stage boost.")]
    public int BoostCRIT { get => (Boost3 >> 4) & 3; set => Boost3 = (byte)((Boost3 & ~0x30) | ((value & 3) << 4)); }

    [Category(Battle), DisplayName("PP Up"), Description("Applies a PP Up-style effect.")]
    public int BoostPP1 { get => (Boost3 >> 6) & 1; set => Boost3 = (byte)((Boost3 & 0xBF) | ((value & 1) << 6)); }

    [Category(Battle), DisplayName("PP Max"), Description("Applies a PP Max-style effect.")]
    public int BoostPPMax { get => (Boost3 >> 7) & 1; set => Boost3 = (byte)((Boost3 & 0x7F) | ((value & 1) << 7)); }

    [Category(Field), DisplayName("Use Flags 1"), Description("Field-use flags for HP restore, PP restore, and EV-gain behavior.")]
    public ItemFlags1 FunctionFlags0
    {
        get => (ItemFlags1)Data[0x23];
        set => Data[0x23] = (byte)value;
    }

    [Category(Field), DisplayName("Use Flags 2"), Description("Field-use flags for Sp. Def EV, friendship, and related behavior.")]
    public ItemFlags2 FunctionFlags1
    {
        get => (ItemFlags2)Data[0x24];
        set => Data[0x24] = (byte)value;
    }

    [Category(Pokemon), DisplayName("HP EV"), Description("EVs added to HP.")]
    public sbyte EVHP { get => (sbyte)Data[0x25]; set => Data[0x25] = (byte)value; }

    [Category(Pokemon), DisplayName("Attack EV"), Description("EVs added to Attack.")]
    public sbyte EVATK { get => (sbyte)Data[0x26]; set => Data[0x26] = (byte)value; }

    [Category(Pokemon), DisplayName("Defense EV"), Description("EVs added to Defense.")]
    public sbyte EVDEF { get => (sbyte)Data[0x27]; set => Data[0x27] = (byte)value; }

    [Category(Pokemon), DisplayName("Speed EV"), Description("EVs added to Speed.")]
    public sbyte EVSPE { get => (sbyte)Data[0x28]; set => Data[0x28] = (byte)value; }

    [Category(Pokemon), DisplayName("Sp. Atk EV"), Description("EVs added to Sp. Atk.")]
    public sbyte EVSPA { get => (sbyte)Data[0x29]; set => Data[0x29] = (byte)value; }

    [Category(Pokemon), DisplayName("Sp. Def EV"), Description("EVs added to Sp. Def.")]
    public sbyte EVSPD { get => (sbyte)Data[0x2A]; set => Data[0x2A] = (byte)value; }

    [Category(Pokemon), DisplayName("Heal Amount"), Description("HP restored. Numeric values are flat HP; enum values can represent fractional/full heals."), RefreshProperties(RefreshProperties.All)]
    public Heal HealAmount { get => (Heal)Data[0x2B]; set => Data[0x2B] = (byte)value; }

    [Category(Pokemon), DisplayName("Heal Value"), Description("Raw heal value. Values under 253 are flat HP; 253=Quarter, 254=Half, 255=Full."), RefreshProperties(RefreshProperties.All)]
    public int HealValue { get => (int)HealAmount; set => HealAmount = (Heal)value; }

    [Category(Pokemon), DisplayName("PP Gain"), Description("PP restored to a move when used.")]
    public byte PPGain { get => Data[0x2C]; set => Data[0x2C] = value; }

    [Category(Pokemon), DisplayName("Friendship Gain 1"), Description("Friendship gained at the first friendship threshold.")]
    public sbyte FriendshipGain1 { get => (sbyte)Data[0x2D]; set => Data[0x2D] = (byte)value; }

    [Category(Pokemon), DisplayName("Friendship Gain 2"), Description("Friendship gained at the second friendship threshold.")]
    public sbyte FriendshipGain2 { get => (sbyte)Data[0x2E]; set => Data[0x2E] = (byte)value; }

    [Category(Pokemon), DisplayName("Friendship Gain 3"), Description("Friendship gained at the third friendship threshold.")]
    public sbyte FriendshipGain3 { get => (sbyte)Data[0x2F]; set => Data[0x2F] = (byte)value; }

    private void SetMachineSlot(int slot)
    {
        GroupType = GroupIndexType.TM;
        Pouch = PouchID.TMs;
        EffectField = FieldItemType.TM;
        ItemType = 7;
        GroupIndex = (byte)Math.Clamp(slot, 0, LastMachineSlot);
    }

    public sealed class MachineMoveConverter() : EnumConverter(typeof(Move))
    {
        private static readonly StandardValuesCollection DefaultChoices = new(Item8MachineTable.CreateDefault().GetMoveChoices());

        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        {
            return context?.Instance is Item8 item
                ? new StandardValuesCollection(item.MachineTable.GetMoveChoices())
                : DefaultChoices;
        }
    }

    public static Item8[] GetArray(ReadOnlySpan<byte> bin, IReadOnlyList<ushort>? allowedMachineMoves = null)
    {
        int numEntries = ReadUInt16LittleEndian(bin);
        int maxEntryIndex = ReadUInt16LittleEndian(bin[4..]);
        int entriesStart = ReadInt32LittleEndian(bin[0x40..]);
        var machineTable = Item8MachineTable.FromItemData(bin, allowedMachineMoves);
        var result = new Item8[numEntries];
        for (var i = 0; i < result.Length; i++)
        {
            var entryIndex = ReadUInt16LittleEndian(bin[(0x44 + (2 * i))..]);
            if (entryIndex >= maxEntryIndex)
                throw new IndexOutOfRangeException();

            var ofs = entriesStart + (entryIndex * SIZE);
            result[i] = new Item8(i, bin.Slice(ofs, SIZE).ToArray(), machineTable);
        }

        return result;
    }

    public static byte[] SetArray(ReadOnlySpan<Item8> array, ReadOnlySpan<byte> bin)
    {
        int numEntries = ReadUInt16LittleEndian(bin);
        if (array.Length != numEntries)
            throw new ArgumentException("Incompatible sizes");

        var result = bin.ToArray();
        if (array.Length != 0)
            array[0].MachineTable.WriteTo(result);

        int maxEntryIndex = ReadUInt16LittleEndian(bin[4..]);
        int entriesStart = ReadInt32LittleEndian(bin[0x40..]);
        for (int i = 0; i < array.Length; i++)
        {
            var entryIndex = ReadUInt16LittleEndian(bin[(0x44 + (2 * i))..]);
            if (entryIndex >= maxEntryIndex)
                throw new IndexOutOfRangeException();

            var data = array[i].Data;
            var ofs = entriesStart + (entryIndex * SIZE);
            var span = result.AsSpan(ofs, SIZE);
            data.CopyTo(span);
        }

        return result;
    }

    public enum PouchID : byte
    {
        Medicine,
        Balls,
        Battle,
        Berries,
        Items,
        TMs,
        Treasures,
        Ingredients,
        Key,
    }

    public enum GroupIndexType : byte
    {
        None = 0,
        Ball = 1,
        _2 = 2, // unused?
        Berries = 3,
        TM = 4,
        Gems = 5, // only for Normal Gem, rest are unused items
    }

    public enum MachineKind : byte
    {
        None,
        TM,
        TR,
    }
}
