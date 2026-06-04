using pkNX.Structures;

namespace pkNX.Structures.FlatBuffers.SWSH;

public partial class Rental
{
    public Species SpeciesID
    {
        get => (Species)Species;
        set => Species = (int)value;
    }

    public Ball BallID
    {
        get => (Ball)Ball;
        set => Ball = (int)value;
    }

    public int ItemID
    {
        get => Item;
        set => Item = value;
    }

    public Nature NatureID
    {
        get => (Nature)Nature;
        set => Nature = (int)value;
    }

    public FixedGender GenderType
    {
        get => (FixedGender)Gender;
        set => Gender = (int)value;
    }

    public RentalAbilitySlot AbilitySlot
    {
        get => (RentalAbilitySlot)Ability;
        set => Ability = (int)value;
    }

    public Move MoveSlot1
    {
        get => (Move)Move1;
        set => Move1 = (int)value;
    }

    public Move MoveSlot2
    {
        get => (Move)Move2;
        set => Move2 = (int)value;
    }

    public Move MoveSlot3
    {
        get => (Move)Move3;
        set => Move3 = (int)value;
    }

    public Move MoveSlot4
    {
        get => (Move)Move4;
        set => Move4 = (int)value;
    }
}

public enum RentalAbilitySlot
{
    Ability1 = 0,
    Ability2 = 1,
    HiddenAbility = 2,
}
