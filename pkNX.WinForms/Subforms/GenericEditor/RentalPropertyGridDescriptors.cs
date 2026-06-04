using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using pkNX.Structures;
using pkNX.Structures.FlatBuffers.SWSH;

namespace pkNX.WinForms;

public static class RentalPropertyGridUtil
{
    private static string[] SpeciesNames = [];
    private static string[] MoveNames = [];

    public static void Configure(IReadOnlyList<string> speciesNames, IReadOnlyList<string> moveNames)
    {
        SpeciesNames = speciesNames.ToArray();
        MoveNames = moveNames.ToArray();
    }

    public static bool IsRentalType(Type type) => typeof(Rental).IsAssignableFrom(type) || type.Name == "Rental";

    public static string GetRentalName(Rental rental, int index)
    {
        var species = GetIndexedName(SpeciesNames, rental.Species, includeID: false);
        var form = rental.Form == 0 ? string.Empty : $"-{rental.Form}";
        var moves = string.Join(", ", new[] { rental.Move1, rental.Move2, rental.Move3, rental.Move4 }
            .Where(z => z > 0)
            .Take(2)
            .Select(GetMoveName));
        var suffix = moves.Length == 0 ? string.Empty : $" | {moves}";
        return $"{index:000} {species}{form} Lv{rental.Level}{suffix}";
    }

    public static bool ShouldHide(Type componentType, string propertyName)
    {
        return IsRentalType(componentType) && propertyName is
            "Ability" or
            "Ball" or
            "Gender" or
            "Item" or
            "Move1" or
            "Move2" or
            "Move3" or
            "Move4" or
            "Nature" or
            "Species";
    }

    public static string GetDisplayName(Type componentType, string propertyName)
    {
        if (!IsRentalType(componentType))
            return propertyName;

        return propertyName switch
        {
            "SpeciesID" => "Species",
            "Form" => "Form",
            "Level" => "Level",
            "BallID" => "Ball",
            "ItemID" => "Held Item",
            "NatureID" => "Nature",
            "GenderType" => "Gender",
            "AbilitySlot" => "Ability",
            "MoveSlot1" => "Move 1",
            "MoveSlot2" => "Move 2",
            "MoveSlot3" => "Move 3",
            "MoveSlot4" => "Move 4",
            "EVHP" => "HP EV",
            "EVATK" => "Attack EV",
            "EVDEF" => "Defense EV",
            "EVSPA" => "Sp. Atk EV",
            "EVSPD" => "Sp. Def EV",
            "EVSPE" => "Speed EV",
            "IVHP" => "HP IV",
            "IVATK" => "Attack IV",
            "IVDEF" => "Defense IV",
            "IVSPA" => "Sp. Atk IV",
            "IVSPD" => "Sp. Def IV",
            "IVSPE" => "Speed IV",
            "Hash1" => "Internal Hash 1",
            "Hash2" => "Internal Hash 2",
            "TrainerID" => "Trainer ID",
            _ => propertyName,
        };
    }

    public static string GetDescription(Type componentType, string propertyName)
    {
        if (!IsRentalType(componentType))
            return string.Empty;

        return propertyName switch
        {
            "SpeciesID" => "Pokemon species for this rental.",
            "Form" => "Form index for the selected species. Zero is the default form.",
            "Level" => "Rental Pokemon level.",
            "BallID" => "Ball stored for this rental Pokemon.",
            "ItemID" => "Held item assigned to this rental Pokemon. Zero means no held item.",
            "NatureID" => "Nature assigned to this rental Pokemon.",
            "GenderType" => "Fixed gender behavior for this rental Pokemon.",
            "AbilitySlot" => "Ability slot used by this rental Pokemon: Ability 1, Ability 2, or Hidden Ability.",
            "MoveSlot1" => "First move used by this rental Pokemon.",
            "MoveSlot2" => "Second move used by this rental Pokemon.",
            "MoveSlot3" => "Third move used by this rental Pokemon.",
            "MoveSlot4" => "Fourth move used by this rental Pokemon.",
            "EVHP" or "EVATK" or "EVDEF" or "EVSPA" or "EVSPD" or "EVSPE" => "Effort value assigned to this stat.",
            "IVHP" or "IVATK" or "IVDEF" or "IVSPA" or "IVSPD" or "IVSPE" => "Individual value assigned to this stat. Base rental data usually uses fixed values.",
            "Hash1" => "Unresolved internal rental reference/hash. Kept editable for advanced use until its source is fully mapped.",
            "Hash2" => "Unresolved internal rental reference/hash. Kept editable for advanced use until its source is fully mapped.",
            "TrainerID" => "Trainer ID field. Base rental data appears to leave this as zero.",
            _ => string.Empty,
        };
    }

    public static string GetCategory(Type componentType, string propertyName)
    {
        if (!IsRentalType(componentType))
            return string.Empty;

        return propertyName switch
        {
            "SpeciesID" or "Form" or "Level" or "BallID" or "ItemID" or "NatureID" or "GenderType" or "AbilitySlot" => "Pokemon",
            "MoveSlot1" or "MoveSlot2" or "MoveSlot3" or "MoveSlot4" => "Moves",
            "EVHP" or "EVATK" or "EVDEF" or "EVSPA" or "EVSPD" or "EVSPE" => "EVs",
            "IVHP" or "IVATK" or "IVDEF" or "IVSPA" or "IVSPD" or "IVSPE" => "IVs",
            "Hash1" or "Hash2" or "TrainerID" => "Internal",
            _ => "Raw / Unknown",
        };
    }

    public static TypeConverter? GetConverter(Type componentType, string propertyName)
    {
        if (!IsRentalType(componentType))
            return null;

        return propertyName switch
        {
            "SpeciesID" => new RaidNamedEnumConverter<Species>(SpeciesNames),
            "MoveSlot1" or "MoveSlot2" or "MoveSlot3" or "MoveSlot4" => new RaidNamedEnumConverter<Move>(MoveNames),
            "ItemID" => new RentalItemValueConverter(),
            "AbilitySlot" => new RentalAbilitySlotConverter(),
            "Hash1" or "Hash2" => new RaidUInt64HexConverter(),
            _ => null,
        };
    }

    private static string GetMoveName(int move) => GetIndexedName(MoveNames, move, includeID: false);

    private static string GetIndexedName(IReadOnlyList<string> names, int index, bool includeID)
    {
        if ((uint)index < (uint)names.Count && !string.IsNullOrWhiteSpace(names[index]))
            return includeID ? $"{names[index]} ({index})" : names[index];

        return index.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed class RentalPropertyDescriptor(PropertyDescriptor baseDescriptor) : PropertyDescriptor(baseDescriptor)
{
    public override bool CanResetValue(object component) => baseDescriptor.CanResetValue(component);
    public override Type ComponentType => baseDescriptor.ComponentType;
    public override string Category => RentalPropertyGridUtil.GetCategory(ComponentType, Name);
    public override TypeConverter Converter => RentalPropertyGridUtil.GetConverter(ComponentType, Name) ?? baseDescriptor.Converter;
    public override string Description => RentalPropertyGridUtil.GetDescription(ComponentType, Name);
    public override string DisplayName => RentalPropertyGridUtil.GetDisplayName(ComponentType, Name);
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

public sealed class RentalItemValueConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text && TryParseItemID(text, out var itemID))
            return itemID;

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is int itemID)
            return itemID == 0 ? "None (0)" : ShopItemNameFormatter.GetDisplayName(itemID, includeID: true);

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var values = Enumerable.Range(0, pkNX.Structures.ItemConverter.ItemNames.Length)
            .ToArray();
        return new StandardValuesCollection(values);
    }

    private static bool TryParseItemID(string text, out int itemID)
    {
        text = text.Trim();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemID))
            return true;

        var open = text.LastIndexOf('(');
        var close = text.LastIndexOf(')');
        if (open >= 0 && close > open && int.TryParse(text[(open + 1)..close], NumberStyles.Integer, CultureInfo.InvariantCulture, out itemID))
            return true;

        var nameOnly = open > 0 ? text[..open].Trim() : text;
        var names = pkNX.Structures.ItemConverter.ItemNames;
        for (var i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], nameOnly, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ShopItemNameFormatter.GetDisplayName(i), nameOnly, StringComparison.OrdinalIgnoreCase))
            {
                itemID = i;
                return true;
            }
        }

        itemID = 0;
        return false;
    }
}

public sealed class RentalAbilitySlotConverter : EnumConverter
{
    public RentalAbilitySlotConverter() : base(typeof(RentalAbilitySlot)) { }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is RentalAbilitySlot slot)
        {
            return slot switch
            {
                RentalAbilitySlot.Ability1 => "Ability 1 (0)",
                RentalAbilitySlot.Ability2 => "Ability 2 (1)",
                RentalAbilitySlot.HiddenAbility => "Hidden Ability (2)",
                _ => $"{slot} ({(int)slot})",
            };
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (text.StartsWith("Ability 1", StringComparison.OrdinalIgnoreCase))
                return RentalAbilitySlot.Ability1;
            if (text.StartsWith("Ability 2", StringComparison.OrdinalIgnoreCase))
                return RentalAbilitySlot.Ability2;
            if (text.StartsWith("Hidden", StringComparison.OrdinalIgnoreCase))
                return RentalAbilitySlot.HiddenAbility;
        }

        return base.ConvertFrom(context, culture, value);
    }
}
