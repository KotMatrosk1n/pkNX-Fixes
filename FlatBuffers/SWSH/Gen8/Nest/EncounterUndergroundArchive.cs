using pkNX.Structures;

namespace pkNX.Structures.FlatBuffers.SWSH;

public partial class EncounterUnderground
{
    public Species SpeciesID
    {
        get => (Species)Species;
        set => Species = (int)value;
    }

    public Ball BallID
    {
        get => (Ball)Ball;
        set => Ball = (uint)value;
    }

    public RaidAbilityRoll AbilitySlot
    {
        get => (RaidAbilityRoll)Ability;
        set => Ability = (uint)value;
    }

    public DynamaxAdventureGigantamaxState Gigantamax
    {
        get => (DynamaxAdventureGigantamaxState)GigantamaxState;
        set => GigantamaxState = (uint)value;
    }

    public DynamaxAdventureVersion GameVersion
    {
        get => (DynamaxAdventureVersion)Version;
        set => Version = (byte)value;
    }

    public DynamaxAdventureShinyRoll ShinyRoll
    {
        get => (DynamaxAdventureShinyRoll)Shiny;
        set => Shiny = (uint)value;
    }

    public Move MoveSlot1
    {
        get => (Move)Move0;
        set => Move0 = (uint)value;
    }

    public Move MoveSlot2
    {
        get => (Move)Move1;
        set => Move1 = (uint)value;
    }

    public Move MoveSlot3
    {
        get => (Move)Move2;
        set => Move2 = (uint)value;
    }

    public Move MoveSlot4
    {
        get => (Move)Move3;
        set => Move3 = (uint)value;
    }

    public int GuaranteedPerfectIVs
    {
        get => IVHP < -1 ? -IVHP : 0;
        set => IVHP = (sbyte)(value <= 0 ? -1 : -Math.Clamp(value, 1, 6));
    }

    public int Gender => 0; // Random
    public bool IsGigantamax => GigantamaxState == 2;

    public override string ToString() => $"{IndexNum:000} - {(Species)Species}{(Form == 0 ? string.Empty : "-" + Form)}";

    public string GetSummary(IReadOnlyList<string> species)
    {
        var gender = Gender == 0 ? string.Empty : $", Gender = {Gender - 1}";
        var comment = $" // {species[Species]}{(Form == 0 ? string.Empty : "-" + Form)}";
        var moves = $", Moves = new[] {{{Move0:000},{Move1:000},{Move2:000},{Move3:000}}}";
        var game = Version != 0 ? Version == 1 ? ", Version = GameVersion.SW" : ", Version = GameVersion.SH" : "";
        var g = IsGigantamax ? ", CanGigantamax = true" : "";
        return $"            new({Species:000},{Form},{Level:00}) {{ Ability = A{Ability}{gender}{moves}{g}{game} }},{comment}";
    }
}

public enum DynamaxAdventureGigantamaxState : uint
{
    Unknown = 0,
    Normal = 1,
    Gigantamax = 2,
}

public enum DynamaxAdventureVersion : byte
{
    Both = 0,
    Sword = 1,
    Shield = 2,
}

public enum DynamaxAdventureShinyRoll : uint
{
    Unknown = 0,
    Enabled = 1,
    Disabled = 2,
}
