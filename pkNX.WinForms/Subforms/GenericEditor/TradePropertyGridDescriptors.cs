using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using pkNX.Structures;
using SWSHEncounterTrade = pkNX.Structures.FlatBuffers.SWSH.EncounterTrade;
using TradeAbilitySlot = pkNX.Structures.FlatBuffers.SWSH.TradeAbilitySlot;

namespace pkNX.WinForms;

public static class TradePropertyGridUtil
{
    private static string[] SpeciesNames = [];
    private static string[] MoveNames = [];

    public static void Configure(IReadOnlyList<string> speciesNames, IReadOnlyList<string> moveNames)
    {
        SpeciesNames = speciesNames.ToArray();
        MoveNames = moveNames.ToArray();
    }

    public static bool IsTradeType(Type type) => typeof(SWSHEncounterTrade).IsAssignableFrom(type) || type.FullName == "pkNX.Structures.FlatBuffers.SWSH.EncounterTrade";

    public static string GetTradeName(SWSHEncounterTrade trade, int index)
    {
        var requested = FormatSpecies(trade.RequiredSpecies, trade.RequiredForm, includeID: false);
        var received = FormatSpecies(trade.Species, trade.Form, includeID: false);
        return $"{index:000} {requested} -> {received} Lv{trade.Level}";
    }

    public static bool ShouldHide(Type componentType, string propertyName)
    {
        return IsTradeType(componentType) && propertyName is
            "AbilityNumber" or
            "BallItemID" or
            "Gender" or
            "HeldItem" or
            "IVs" or
            "Nature" or
            "RequiredNature" or
            "RequiredSpecies" or
            "Relearn1" or
            "Relearn2" or
            "Relearn3" or
            "Relearn4" or
            "ShinyLock" or
            "Species";
    }

    public static string GetDisplayName(Type componentType, string propertyName)
    {
        if (!IsTradeType(componentType))
            return propertyName;

        return propertyName switch
        {
            "SpeciesID" => "Received Species",
            "Form" => "Received Form",
            "Level" => "Received Level",
            "Ball" => "Ball",
            "HeldItemID" => "Held Item",
            "NatureID" => "Nature",
            "GenderType" => "Gender",
            "AbilitySlot" => "Ability",
            "ShinyType" => "Shiny Lock",
            "CanGigantamax" => "Can Gigantamax",
            "DynamaxLevel" => "Dynamax Level",
            "RequiredSpeciesID" => "Requested Species",
            "RequiredForm" => "Requested Form",
            "RequiredNatureID" => "Requested Nature",
            "UnknownRequirement" => "Unknown Requirement Flag",
            "RelearnMove1" => "Relearn Move 1",
            "RelearnMove2" => "Relearn Move 2",
            "RelearnMove3" => "Relearn Move 3",
            "RelearnMove4" => "Relearn Move 4",
            "IVHP" => "HP IV",
            "IVATK" => "Attack IV",
            "IVDEF" => "Defense IV",
            "IVSPA" => "Sp. Atk IV",
            "IVSPD" => "Sp. Def IV",
            "IVSPE" => "Speed IV",
            "TrainerID" => "Trainer ID",
            "OTGender" => "OT Gender",
            "Memory" => "Memory Code",
            "TextVar" => "Memory Text Variable",
            "Feeling" => "Memory Feeling",
            "Intensity" => "Memory Intensity",
            "Hash0" => "Internal Hash 0",
            "Hash1" => "Internal Hash 1",
            "Hash2" => "Internal Hash 2",
            "Field03" => "Unknown Field 03",
            _ => MovePropertyGridUtil.SplitGeneratedName(propertyName),
        };
    }

    public static string GetDescription(Type componentType, string propertyName)
    {
        if (!IsTradeType(componentType))
            return string.Empty;

        return propertyName switch
        {
            "SpeciesID" => "Pokemon the player receives from the NPC trade.",
            "Form" => "Form index for the received Pokemon. Zero is the default form.",
            "Level" => "Level of the received Pokemon.",
            "Ball" => "Ball stored on the received Pokemon.",
            "HeldItemID" => "Held item assigned to the received Pokemon. Zero means no held item.",
            "NatureID" => "Nature assigned to the received Pokemon. Random uses the game's random nature behavior.",
            "GenderType" => "Fixed gender behavior for the received Pokemon.",
            "AbilitySlot" => "Ability slot used by the received Pokemon: default, Ability 1, Ability 2, or Hidden Ability.",
            "ShinyType" => "Shiny behavior for the received Pokemon: random, forced shiny, or forced not shiny.",
            "CanGigantamax" => "Whether the received Pokemon can Gigantamax when its species supports it.",
            "DynamaxLevel" => "Dynamax level assigned to the received Pokemon.",
            "RequiredSpeciesID" => "Pokemon species the NPC asks the player to trade away.",
            "RequiredForm" => "Required form index for the Pokemon the player must trade away. Zero is the default form.",
            "RequiredNatureID" => "Required nature for the Pokemon the player must trade away. Random/25 means any nature.",
            "UnknownRequirement" => "Unmapped trade requirement flag. Base Sword/Shield data stores this as zero.",
            "RelearnMove1" => "First relearn move assigned to the received Pokemon. None leaves this slot empty.",
            "RelearnMove2" => "Second relearn move assigned to the received Pokemon. None leaves this slot empty.",
            "RelearnMove3" => "Third relearn move assigned to the received Pokemon. None leaves this slot empty.",
            "RelearnMove4" => "Fourth relearn move assigned to the received Pokemon. None leaves this slot empty.",
            "IVHP" => "Individual value assigned to HP. -1 means random; -4 is used by base data as a sentinel for three randomly chosen perfect IVs.",
            "IVATK" or "IVDEF" or "IVSPA" or "IVSPD" or "IVSPE" => "Individual value assigned to this stat. -1 means random; 0-31 forces that IV.",
            "TrainerID" => "Trainer ID stored on the received Pokemon.",
            "OTGender" => "Original Trainer gender flag stored on the received Pokemon.",
            "Memory" => "Memory code used for the received Pokemon's OT memory data.",
            "TextVar" => "Text variable used by the received Pokemon's memory/dialogue data.",
            "Feeling" => "Feeling value used by the received Pokemon's memory data.",
            "Intensity" => "Intensity value used by the received Pokemon's memory data.",
            "Hash0" or "Hash1" or "Hash2" => "Unresolved internal trade hash/reference. Kept editable for advanced testing.",
            "Field03" => "Unmapped trade field. Base Sword/Shield data stores this as zero.",
            _ => string.Empty,
        };
    }

    public static string GetCategory(Type componentType, string propertyName)
    {
        if (!IsTradeType(componentType))
            return string.Empty;

        return propertyName switch
        {
            "SpeciesID" or "Form" or "Level" or "Ball" or "HeldItemID" or "NatureID" or "GenderType" or "AbilitySlot" or "ShinyType" or "CanGigantamax" or "DynamaxLevel" => "Received Pokemon",
            "RequiredSpeciesID" or "RequiredForm" or "RequiredNatureID" or "UnknownRequirement" => "Requested Pokemon",
            "RelearnMove1" or "RelearnMove2" or "RelearnMove3" or "RelearnMove4" => "Relearn Moves",
            "IVHP" or "IVATK" or "IVDEF" or "IVSPA" or "IVSPD" or "IVSPE" => "IVs",
            "TrainerID" or "OTGender" or "Memory" or "TextVar" or "Feeling" or "Intensity" => "OT / Memory",
            "Hash0" or "Hash1" or "Hash2" => "Internal References",
            _ => "Raw / Unknown",
        };
    }

    public static TypeConverter? GetConverter(Type componentType, string propertyName)
    {
        if (!IsTradeType(componentType))
            return null;

        return propertyName switch
        {
            "SpeciesID" or "RequiredSpeciesID" => new RaidNamedEnumConverter<Species>(SpeciesNames),
            "Ball" => new TradeBallConverter(),
            "HeldItemID" => new StaticEncounterItemValueConverter(),
            "NatureID" or "RequiredNatureID" => new StaticEncounterNatureConverter(),
            "GenderType" => new TradeGenderConverter(),
            "AbilitySlot" => new TradeAbilitySlotConverter(),
            "ShinyType" => new StaticEncounterShinyConverter(),
            "RelearnMove1" or "RelearnMove2" or "RelearnMove3" or "RelearnMove4" => new RaidNamedEnumConverter<Move>(MoveNames),
            "IVHP" => new StaticEncounterIVConverter(allowFlawlessMarker: true),
            "IVATK" or "IVDEF" or "IVSPA" or "IVSPD" or "IVSPE" => new StaticEncounterIVConverter(allowFlawlessMarker: false),
            "OTGender" => new TradeOTGenderConverter(),
            "Hash0" or "Hash1" or "Hash2" => new RaidUInt64HexConverter(),
            _ => null,
        };
    }

    private static string FormatSpecies(int speciesID, int form, bool includeID)
    {
        var species = GetIndexedName(SpeciesNames, speciesID, includeID);
        return form == 0 ? species : $"{species}-{form}";
    }

    private static string GetIndexedName(IReadOnlyList<string> names, int index, bool includeID)
    {
        if ((uint)index < (uint)names.Count && !string.IsNullOrWhiteSpace(names[index]))
            return includeID ? $"{names[index]} ({index})" : names[index];

        return index.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed class TradePropertyDescriptor(PropertyDescriptor baseDescriptor) : PropertyDescriptor(baseDescriptor)
{
    public override bool CanResetValue(object component) => baseDescriptor.CanResetValue(component);
    public override Type ComponentType => baseDescriptor.ComponentType;
    public override string Category => TradePropertyGridUtil.GetCategory(ComponentType, Name);
    public override TypeConverter Converter => TradePropertyGridUtil.GetConverter(ComponentType, Name) ?? baseDescriptor.Converter;
    public override string Description => TradePropertyGridUtil.GetDescription(ComponentType, Name);
    public override string DisplayName => TradePropertyGridUtil.GetDisplayName(ComponentType, Name);
    public override object? GetValue(object? component) => baseDescriptor.GetValue(component);
    public override bool IsReadOnly => baseDescriptor.IsReadOnly;
    public override Type PropertyType => baseDescriptor.PropertyType;
    public override void ResetValue(object component) => baseDescriptor.ResetValue(component);
    public override void SetValue(object? component, object? value) => baseDescriptor.SetValue(component, value);
    public override bool ShouldSerializeValue(object component) => baseDescriptor.ShouldSerializeValue(component);

    public override object? GetEditor(Type editorBaseType)
    {
        if (editorBaseType == typeof(UITypeEditor) && SearchableStandardValuesUITypeEditor.Supports(null, Converter))
            return new SearchableStandardValuesUITypeEditor();

        return baseDescriptor.GetEditor(editorBaseType);
    }
}

public sealed class TradeBallConverter : EnumConverter
{
    public TradeBallConverter() : base(typeof(Ball)) { }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text && TryParseBall(text, out var ball))
            return ball;

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is Ball ball)
            return $"{MovePropertyGridUtil.SplitGeneratedName(ball.ToString())} ({(int)ball})";

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var values = Enumerable.Range(0, SWSHEncounterTrade.BallToItem.Length)
            .Select(z => (Ball)z)
            .Cast<object>()
            .ToArray();
        return new StandardValuesCollection(values);
    }

    private static bool TryParseBall(string text, out Ball ball)
    {
        text = text.Trim();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            ball = (Ball)numeric;
            return numeric >= 0 && numeric < SWSHEncounterTrade.BallToItem.Length;
        }

        var open = text.LastIndexOf('(');
        var close = text.LastIndexOf(')');
        if (open >= 0 && close > open && int.TryParse(text[(open + 1)..close], NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
        {
            ball = (Ball)numeric;
            return numeric >= 0 && numeric < SWSHEncounterTrade.BallToItem.Length;
        }

        var nameOnly = open > 0 ? text[..open].Trim() : text;
        foreach (var value in Enum.GetValues<Ball>().Where(z => (int)z < SWSHEncounterTrade.BallToItem.Length))
        {
            var name = value.ToString();
            if (string.Equals(name, nameOnly, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(MovePropertyGridUtil.SplitGeneratedName(name), nameOnly, StringComparison.OrdinalIgnoreCase))
            {
                ball = value;
                return true;
            }
        }

        ball = Ball.None;
        return false;
    }
}

public sealed class TradeGenderConverter : EnumConverter
{
    public TradeGenderConverter() : base(typeof(FixedGender)) { }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (text.StartsWith("Random", StringComparison.OrdinalIgnoreCase))
                return FixedGender.Random;
            if (text.StartsWith("Male", StringComparison.OrdinalIgnoreCase))
                return FixedGender.Male;
            if (text.StartsWith("Female", StringComparison.OrdinalIgnoreCase))
                return FixedGender.Female;
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is FixedGender gender)
        {
            return gender switch
            {
                FixedGender.Random => "Random / Any (0)",
                FixedGender.Male => "Male (1)",
                FixedGender.Female => "Female (2)",
                _ => $"{gender} ({(int)gender})",
            };
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        return new StandardValuesCollection(new object[] { FixedGender.Random, FixedGender.Male, FixedGender.Female });
    }
}

public sealed class TradeAbilitySlotConverter : EnumConverter
{
    public TradeAbilitySlotConverter() : base(typeof(TradeAbilitySlot)) { }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (text.StartsWith("Default", StringComparison.OrdinalIgnoreCase))
                return TradeAbilitySlot.Default;
            if (text.StartsWith("Ability 1", StringComparison.OrdinalIgnoreCase))
                return TradeAbilitySlot.Ability1;
            if (text.StartsWith("Ability 2", StringComparison.OrdinalIgnoreCase))
                return TradeAbilitySlot.Ability2;
            if (text.StartsWith("Hidden", StringComparison.OrdinalIgnoreCase))
                return TradeAbilitySlot.HiddenAbility;
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is TradeAbilitySlot slot)
        {
            return slot switch
            {
                TradeAbilitySlot.Default => "Default / No Override (0)",
                TradeAbilitySlot.Ability1 => "Ability 1 (1)",
                TradeAbilitySlot.Ability2 => "Ability 2 (2)",
                TradeAbilitySlot.HiddenAbility => "Hidden Ability (3)",
                _ => $"{slot} ({(int)slot})",
            };
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}

public sealed class TradeOTGenderConverter : ByteConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (text.StartsWith("Male", StringComparison.OrdinalIgnoreCase))
                return (byte)0;
            if (text.StartsWith("Female", StringComparison.OrdinalIgnoreCase))
                return (byte)1;

            var numeric = new string(text.SkipWhile(z => !char.IsDigit(z)).TakeWhile(char.IsDigit).ToArray());
            if (byte.TryParse(numeric, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                return result;
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is byte gender)
        {
            return gender switch
            {
                0 => "Male (0)",
                1 => "Female (1)",
                _ => gender.ToString(CultureInfo.InvariantCulture),
            };
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
