namespace pkNX.Structures.FlatBuffers.SWSH;

public partial class EncounterStaticArchive;

public partial class EncounterStatic
{
    public global::pkNX.Structures.Species SpeciesID
    {
        get => (global::pkNX.Structures.Species)Species;
        set => Species = (int)value;
    }

    public int HeldItemID
    {
        get => HeldItem;
        set => HeldItem = value;
    }

    public global::pkNX.Structures.Nature NatureID
    {
        get => (global::pkNX.Structures.Nature)Nature;
        set => Nature = value == global::pkNX.Structures.Nature.Random ? (uint)global::pkNX.Structures.Nature.Random25 : (uint)value;
    }

    public StaticEncounterGender GenderType
    {
        get => (StaticEncounterGender)Gender;
        set => Gender = (sbyte)value;
    }

    public StaticEncounterAbility AbilitySlot
    {
        get => (StaticEncounterAbility)Ability;
        set => Ability = (int)value;
    }

    public global::pkNX.Structures.Shiny ShinyType
    {
        get => (global::pkNX.Structures.Shiny)ShinyLock;
        set => ShinyLock = (uint)value;
    }

    public global::pkNX.Structures.Move MoveSlot1
    {
        get => (global::pkNX.Structures.Move)Move0;
        set => Move0 = (int)value;
    }

    public global::pkNX.Structures.Move MoveSlot2
    {
        get => (global::pkNX.Structures.Move)Move1;
        set => Move1 = (int)value;
    }

    public global::pkNX.Structures.Move MoveSlot3
    {
        get => (global::pkNX.Structures.Move)Move2;
        set => Move2 = (int)value;
    }

    public global::pkNX.Structures.Move MoveSlot4
    {
        get => (global::pkNX.Structures.Move)Move3;
        set => Move3 = (int)value;
    }

    public int[] IVs
    {
        get => [IVHP, IVATK, IVDEF, IVSPE, IVSPA, IVSPD];
        set
        {
            if (value?.Length != 6) return;
            IVHP = (sbyte)value[0];
            IVATK = (sbyte)value[1];
            IVDEF = (sbyte)value[2];
            IVSPE = (sbyte)value[3];
            IVSPA = (sbyte)value[4];
            IVSPD = (sbyte)value[5];
        }
    }

    public int[] Moves
    {
        get => [Move0, Move1, Move2, Move3];
        set
        {
            if (value?.Length != 4) return;
            Move0 = value[0];
            Move1 = value[1];
            Move2 = value[2];
            Move3 = value[3];
        }
    }

    public string GetSummary(IReadOnlyList<string> species)
    {
        var comment = $" // {species[Species]}{(Form == 0 ? string.Empty : "-" + Form)}";
        var ability = Ability switch
        {
            0 => string.Empty,
            3 => ", Ability = 4",
            _ => $", Ability = {Ability}",
        };

        var ivs = IVs[0] switch
        {
            31 when IVs.All(z => z == 31) => ", FlawlessIVCount = 6",
            -1 when IVs.All(z => z == -1) => string.Empty,
            -4 => ", FlawlessIVCount = 3",
            _ => $", IVs = new[]{{{string.Join(",", IVs)}}}",
        };

        var gender = Gender == (int)FixedGender.Random ? string.Empty : $", Gender = {Gender - 1}";
        var nature = (Nature)Nature == Structures.Nature.Random25 ? string.Empty : $", Nature = Nature.{(Nature)Nature}";
        var altform = Form == 0 ? string.Empty : $", Form = {Form:00}";
        var moves = Move0 == 0 ? string.Empty : $", Moves = new[] {{{Move0:000},{Move1:000},{Move2:000},{Move3:000}}}";
        var shiny = (Shiny)ShinyLock == Shiny.Random ? string.Empty : $", Shiny = {(Shiny)ShinyLock}";
        var giga = !CanGigantamax ? string.Empty : ", CanGigantamax = true";
        var dyna = DynamaxLevel == 0 ? string.Empty : $", DynamaxLevel = {DynamaxLevel}";

        return
            $"            new(SWSH) {{ Species = {Species:000}, Level = {Level:00}, Location = -01{moves}{ivs}{shiny}{gender}{ability}{nature}{altform}{giga}{dyna} }},{comment}";
    }
}

public enum StaticEncounterGender
{
    Random = 0,
    Male = 1,
    Female = 2,
}

public enum StaticEncounterAbility
{
    Default = 0,
    Ability1 = 1,
    Ability2 = 2,
    HiddenAbility = 3,
}
