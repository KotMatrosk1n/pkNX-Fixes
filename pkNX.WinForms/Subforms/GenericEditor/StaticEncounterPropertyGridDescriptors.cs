using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using pkNX.Structures;
using pkNX.Structures.FlatBuffers.SWSH;
using SWSHEncounterStatic = pkNX.Structures.FlatBuffers.SWSH.EncounterStatic;

namespace pkNX.WinForms;

public static class StaticEncounterPropertyGridUtil
{
    private static string[] SpeciesNames = [];
    private static string[] MoveNames = [];

    public static void Configure(IReadOnlyList<string> speciesNames, IReadOnlyList<string> moveNames)
    {
        SpeciesNames = speciesNames.ToArray();
        MoveNames = moveNames.ToArray();
    }

    public static bool IsStaticEncounterType(Type type) => typeof(SWSHEncounterStatic).IsAssignableFrom(type) || type.FullName == "pkNX.Structures.FlatBuffers.SWSH.EncounterStatic";

    public static string GetStaticEncounterName(SWSHEncounterStatic encounter, int index)
    {
        var species = GetIndexedName(SpeciesNames, encounter.Species, includeID: false);
        var form = encounter.Form == 0 ? string.Empty : $"-{encounter.Form}";
        var scenario = encounter.EncounterScenario == Scenario.None ? string.Empty : $" | {MovePropertyGridUtil.SplitGeneratedName(encounter.EncounterScenario.ToString())}";
        var moves = string.Join(", ", new[] { encounter.Move0, encounter.Move1, encounter.Move2, encounter.Move3 }
            .Where(z => z > 0)
            .Take(2)
            .Select(z => GetIndexedName(MoveNames, z, includeID: false)));
        var moveText = moves.Length == 0 ? string.Empty : $" | {moves}";
        return $"{index:000} {species}{form} Lv{encounter.Level}{scenario}{moveText}";
    }

    public static bool ShouldHide(Type componentType, string propertyName)
    {
        return IsStaticEncounterType(componentType) && propertyName is
            "Ability" or
            "Gender" or
            "HeldItem" or
            "IVs" or
            "Moves" or
            "Move0" or
            "Move1" or
            "Move2" or
            "Move3" or
            "Nature" or
            "ShinyLock" or
            "Species";
    }

    public static string GetDisplayName(Type componentType, string propertyName)
    {
        if (!IsStaticEncounterType(componentType))
            return propertyName;

        return propertyName switch
        {
            "SpeciesID" => "Species",
            "Form" => "Form",
            "Level" => "Level",
            "HeldItemID" => "Held Item",
            "NatureID" => "Nature",
            "GenderType" => "Gender",
            "AbilitySlot" => "Ability",
            "ShinyType" => "Shiny Lock",
            "MoveSlot1" => "Move 1",
            "MoveSlot2" => "Move 2",
            "MoveSlot3" => "Move 3",
            "MoveSlot4" => "Move 4",
            "CanGigantamax" => "Can Gigantamax",
            "DynamaxLevel" => "Dynamax Level",
            "EncounterScenario" => "Scenario",
            "EncounterID" => "Encounter ID",
            "BackgroundFarTypeID" => "Background Far Type",
            "BackgroundNearTypeID" => "Background Near Type",
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
            "Field0A" => "Field 0A",
            "Field0C" => "Field 0C",
            _ => MovePropertyGridUtil.SplitGeneratedName(propertyName),
        };
    }

    public static string GetDescription(Type componentType, string propertyName)
    {
        if (!IsStaticEncounterType(componentType))
            return string.Empty;

        return propertyName switch
        {
            "SpeciesID" => "Pokemon species used by this static encounter.",
            "Form" => "Form index for the selected species. Zero is the default form.",
            "Level" => "Encounter level.",
            "HeldItemID" => "Held item assigned to this encounter. Zero means no held item.",
            "NatureID" => "Nature override for this encounter. Random uses the game's random nature behavior.",
            "GenderType" => "Fixed gender behavior for this encounter.",
            "AbilitySlot" => "Ability override: default, first ability, second ability, or hidden ability.",
            "ShinyType" => "Shiny lock behavior: random, forced shiny, or forced not shiny.",
            "MoveSlot1" => "First move override. Zero lets the encounter use its normal move behavior.",
            "MoveSlot2" => "Second move override. Zero lets the encounter use its normal move behavior.",
            "MoveSlot3" => "Third move override. Zero lets the encounter use its normal move behavior.",
            "MoveSlot4" => "Fourth move override. Zero lets the encounter use its normal move behavior.",
            "CanGigantamax" => "Whether this encounter can use its Gigantamax form when the species supports it.",
            "DynamaxLevel" => "Dynamax level assigned to the encounter.",
            "EncounterScenario" => "Story or battle scenario attached to this encounter. Many scenario values only work for encounters designed to use them.",
            "EncounterID" => "Internal encounter hash/ID referenced by other game data.",
            "BackgroundFarTypeID" => "Internal background/environment hash for the far background type.",
            "BackgroundNearTypeID" => "Internal background/environment hash for the near background type.",
            "EVHP" or "EVATK" or "EVDEF" or "EVSPA" or "EVSPD" or "EVSPE" => "Effort value assigned to this stat.",
            "IVHP" => "Individual value assigned to HP. -1 means random; -4 is used by base data as a special guaranteed-perfect-IV marker.",
            "IVATK" or "IVDEF" or "IVSPA" or "IVSPD" or "IVSPE" => "Individual value assigned to this stat. -1 means random; 0-31 forces that IV.",
            "Field0A" or "Field0C" => "Unmapped static encounter field. Kept editable for advanced use until its purpose is confirmed.",
            _ => string.Empty,
        };
    }

    public static string GetCategory(Type componentType, string propertyName)
    {
        if (!IsStaticEncounterType(componentType))
            return string.Empty;

        return propertyName switch
        {
            "SpeciesID" or "Form" or "Level" or "HeldItemID" or "NatureID" or "GenderType" or "AbilitySlot" or "ShinyType" => "Pokemon",
            "MoveSlot1" or "MoveSlot2" or "MoveSlot3" or "MoveSlot4" => "Moves",
            "CanGigantamax" or "DynamaxLevel" or "EncounterScenario" => "Encounter Rules",
            "EVHP" or "EVATK" or "EVDEF" or "EVSPA" or "EVSPD" or "EVSPE" => "EVs",
            "IVHP" or "IVATK" or "IVDEF" or "IVSPA" or "IVSPD" or "IVSPE" => "IVs",
            "EncounterID" or "BackgroundFarTypeID" or "BackgroundNearTypeID" => "References",
            _ => "Raw / Unknown",
        };
    }

    public static TypeConverter? GetConverter(Type componentType, string propertyName)
    {
        if (!IsStaticEncounterType(componentType))
            return null;

        return propertyName switch
        {
            "SpeciesID" => new RaidNamedEnumConverter<Species>(SpeciesNames),
            "MoveSlot1" or "MoveSlot2" or "MoveSlot3" or "MoveSlot4" => new RaidNamedEnumConverter<Move>(MoveNames),
            "HeldItemID" => new StaticEncounterItemValueConverter(),
            "NatureID" => new StaticEncounterNatureConverter(),
            "AbilitySlot" => new StaticEncounterAbilityConverter(),
            "GenderType" => new MoveEnumConverter<StaticEncounterGender>(),
            "ShinyType" => new StaticEncounterShinyConverter(),
            "EncounterScenario" => new MoveEnumConverter<Scenario>(),
            "IVHP" or "IVATK" or "IVDEF" or "IVSPA" or "IVSPD" or "IVSPE" => new StaticEncounterIVConverter(),
            "EncounterID" or "BackgroundFarTypeID" or "BackgroundNearTypeID" => new RaidUInt64HexConverter(),
            _ => null,
        };
    }

    private static string GetIndexedName(IReadOnlyList<string> names, int index, bool includeID)
    {
        if ((uint)index < (uint)names.Count && !string.IsNullOrWhiteSpace(names[index]))
            return includeID ? $"{names[index]} ({index})" : names[index];

        return index.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed class StaticEncounterPropertyDescriptor(PropertyDescriptor baseDescriptor) : PropertyDescriptor(baseDescriptor)
{
    public override bool CanResetValue(object component) => baseDescriptor.CanResetValue(component);
    public override Type ComponentType => baseDescriptor.ComponentType;
    public override string Category => StaticEncounterPropertyGridUtil.GetCategory(ComponentType, Name);
    public override TypeConverter Converter => StaticEncounterPropertyGridUtil.GetConverter(ComponentType, Name) ?? baseDescriptor.Converter;
    public override string Description => StaticEncounterPropertyGridUtil.GetDescription(ComponentType, Name);
    public override string DisplayName => StaticEncounterPropertyGridUtil.GetDisplayName(ComponentType, Name);
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

public sealed class StaticEncounterItemValueConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

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
        var values = Enumerable.Range(0, ItemConverter.ItemNames.Length).ToArray();
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
        var names = ItemConverter.ItemNames;
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

public sealed class StaticEncounterNatureConverter : EnumConverter
{
    public StaticEncounterNatureConverter() : base(typeof(Nature)) { }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (text.StartsWith("Random", StringComparison.OrdinalIgnoreCase))
                return Nature.Random25;

            if (TryParseNature(text, out var nature))
                return nature == Nature.Random ? Nature.Random25 : nature;
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is Nature nature)
            return nature == Nature.Random25 ? "Random (25)" : $"{nature} ({(int)nature})";

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var values = Enum.GetValues<Nature>()
            .Where(z => z != Nature.Random)
            .Cast<object>()
            .ToArray();
        return new StandardValuesCollection(values);
    }

    private static bool TryParseNature(string text, out Nature nature)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            nature = (Nature)numeric;
            return true;
        }

        var open = text.LastIndexOf('(');
        var close = text.LastIndexOf(')');
        if (open >= 0 && close > open && int.TryParse(text[(open + 1)..close], NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
        {
            nature = (Nature)numeric;
            return true;
        }

        var nameOnly = open > 0 ? text[..open].Trim() : text;
        return Enum.TryParse(nameOnly, true, out nature);
    }
}

public sealed class StaticEncounterAbilityConverter : EnumConverter
{
    public StaticEncounterAbilityConverter() : base(typeof(StaticEncounterAbility)) { }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is StaticEncounterAbility ability)
        {
            return ability switch
            {
                StaticEncounterAbility.Default => "Default / No Override (0)",
                StaticEncounterAbility.Ability1 => "Ability 1 (1)",
                StaticEncounterAbility.Ability2 => "Ability 2 (2)",
                StaticEncounterAbility.HiddenAbility => "Hidden Ability (3)",
                _ => $"{ability} ({(int)ability})",
            };
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (text.StartsWith("Default", StringComparison.OrdinalIgnoreCase))
                return StaticEncounterAbility.Default;
            if (text.StartsWith("Ability 1", StringComparison.OrdinalIgnoreCase))
                return StaticEncounterAbility.Ability1;
            if (text.StartsWith("Ability 2", StringComparison.OrdinalIgnoreCase))
                return StaticEncounterAbility.Ability2;
            if (text.StartsWith("Hidden", StringComparison.OrdinalIgnoreCase))
                return StaticEncounterAbility.HiddenAbility;
        }

        return base.ConvertFrom(context, culture, value);
    }
}

public sealed class StaticEncounterShinyConverter : EnumConverter
{
    public StaticEncounterShinyConverter() : base(typeof(Shiny)) { }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is Shiny shiny)
        {
            return shiny switch
            {
                Shiny.Random => "Random / Can Be Shiny (0)",
                Shiny.Always => "Always Shiny (1)",
                Shiny.Never => "Never Shiny (2)",
                _ => $"{shiny} ({(int)shiny})",
            };
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (text.StartsWith("Random", StringComparison.OrdinalIgnoreCase))
                return Shiny.Random;
            if (text.StartsWith("Always", StringComparison.OrdinalIgnoreCase))
                return Shiny.Always;
            if (text.StartsWith("Never", StringComparison.OrdinalIgnoreCase))
                return Shiny.Never;
        }

        return base.ConvertFrom(context, culture, value);
    }
}

public sealed class StaticEncounterIVConverter : SByteConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (text.StartsWith("Random", StringComparison.OrdinalIgnoreCase))
                return (sbyte)-1;
            if (text.StartsWith("3 Perfect", StringComparison.OrdinalIgnoreCase))
                return (sbyte)-4;

            var numeric = new string(text
                .SkipWhile(z => z != '-' && z != '+' && !char.IsDigit(z))
                .TakeWhile(z => z == '-' || z == '+' || char.IsDigit(z))
                .ToArray());
            if (sbyte.TryParse(numeric, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                return result;
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is sbyte iv)
        {
            return iv switch
            {
                -1 => "Random (-1)",
                -4 => "3 Perfect IV Marker (-4)",
                _ => iv.ToString(CultureInfo.InvariantCulture),
            };
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
