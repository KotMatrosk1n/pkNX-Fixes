using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using pkNX.Structures;
using pkNX.Structures.FlatBuffers.SWSH;

namespace pkNX.WinForms;

public static class RaidPropertyGridUtil
{
    private static readonly Dictionary<ulong, string> EncounterTableLabels = [];
    private static readonly Dictionary<ulong, string> EncounterTableUsageLabels = [];
    private static readonly Dictionary<ulong, string> LevelTableLabels = [];
    private static readonly Dictionary<ulong, string> DropRewardTableLabels = [];
    private static readonly Dictionary<ulong, string> BonusRewardTableLabels = [];
    private static string[] DynamaxAdventureSpeciesNames = [];
    private static string[] DynamaxAdventureMoveNames = [];

    public static void Configure(
        IReadOnlyList<EncounterNestTable> encounterTables,
        IReadOnlyList<ulong> levelTableIDs,
        IReadOnlyList<ulong> dropRewardTableIDs,
        IReadOnlyList<ulong> bonusRewardTableIDs,
        IReadOnlyDictionary<ulong, string>? encounterTableUsageLabels = null)
    {
        EncounterTableUsageLabels.Clear();
        if (encounterTableUsageLabels != null)
        {
            foreach (var (tableID, label) in encounterTableUsageLabels)
                EncounterTableUsageLabels[tableID] = label;
        }

        EncounterTableLabels.Clear();
        for (int i = 0; i < encounterTables.Count; i++)
            EncounterTableLabels.TryAdd(encounterTables[i].TableID, GetRaidTableIDLabel(encounterTables[i], i));

        ConfigureTableLabels(LevelTableLabels, "Level Table", levelTableIDs);
        ConfigureTableLabels(DropRewardTableLabels, "Drop Rewards", dropRewardTableIDs);
        ConfigureTableLabels(BonusRewardTableLabels, "Bonus Rewards", bonusRewardTableIDs);
    }

    public static string GetRaidTableName(EncounterNestTable table, int index)
    {
        var version = table.GameVersion switch
        {
            1 => "Sword",
            2 => "Shield",
            _ => $"Version {table.GameVersion}",
        };

        return $"{version} - {index / 2}";
    }

    public static void ConfigureDynamaxAdventure(IReadOnlyList<string> speciesNames, IReadOnlyList<string> moveNames)
    {
        DynamaxAdventureSpeciesNames = speciesNames.ToArray();
        DynamaxAdventureMoveNames = moveNames.ToArray();
    }

    public static string GetDynamaxAdventureName(EncounterUnderground encounter, int index)
    {
        var species = GetIndexedName(DynamaxAdventureSpeciesNames, encounter.Species);
        var form = encounter.Form == 0 ? string.Empty : $" Form {encounter.Form}";
        var version = encounter.Version switch
        {
            1 => " [Sword]",
            2 => " [Shield]",
            _ => string.Empty,
        };
        return $"{index:000} - {species}{form}{version}";
    }

    private static string GetRaidTableIDLabel(EncounterNestTable table, int index) => $"Den Table {index / 2}{GetUsageSuffix(table.TableID)}";

    private static string GetUsageSuffix(ulong tableID) => EncounterTableUsageLabels.TryGetValue(tableID, out var label) ? $" | {label}" : string.Empty;

    public static bool IsEncounterNestTable(Type type) => typeof(EncounterNestTable).IsAssignableFrom(type) || type.Name == "EncounterNestTable";

    public static bool IsEncounterNest(Type type) => typeof(EncounterNest).IsAssignableFrom(type) || type.Name == "EncounterNest";

    public static bool IsDynamaxAdventure(Type type) => typeof(EncounterUnderground).IsAssignableFrom(type) || type.Name == "EncounterUnderground";

    public static bool IsRaidType(Type type)
    {
        return IsEncounterNestTable(type) || IsEncounterNest(type) || IsDynamaxAdventure(type);
    }

    public static bool ShouldHide(Type componentType, string propertyName)
    {
        if (!IsRaidType(componentType))
            return false;

        if (IsEncounterNestTable(componentType))
            return propertyName is "GameVersion";

        return IsEncounterNest(componentType) && propertyName is
            "Ability" or
            "AbilityPermitted" or
            "Gender" or
            "Probabilities" or
            "Species" ||
            IsDynamaxAdventure(componentType) && propertyName is
            "Ability" or
            "Ball" or
            "Field02" or
            "Gender" or
            "GigantamaxState" or
            "IVHP" or
            "IsGigantamax" or
            "Move0" or
            "Move1" or
            "Move2" or
            "Move3" or
            "Shiny" or
            "Species" or
            "Version";
    }

    public static string GetDisplayName(Type componentType, string propertyName)
    {
        if (!IsRaidType(componentType))
            return propertyName;

        if (IsEncounterNestTable(componentType))
        {
            return propertyName switch
            {
                "TableID" => "Encounter Table ID",
                "Version" => "Game Version",
                "EntryCount" => "Raid Slot Count",
                "Entries" => "Raid Slots",
                _ => propertyName,
            };
        }

        if (IsEncounterNest(componentType))
        {
            return propertyName switch
            {
                "EntryIndex" => "Slot",
                "SpeciesID" => "Species",
                "Form" => "Form",
                "LevelTableID" => "Level Table",
                "AbilityRoll" => "Ability Roll",
                "IsGigantamax" => "Can Gigantamax",
                "DropTableID" => "Drop Reward Table",
                "BonusTableID" => "Bonus Reward Table",
                "Star1Probability" => "1-Star Chance",
                "Star2Probability" => "2-Star Chance",
                "Star3Probability" => "3-Star Chance",
                "Star4Probability" => "4-Star Chance",
                "Star5Probability" => "5-Star Chance",
                "FlawlessIVs" => "Guaranteed Perfect IVs",
                "GenderType" => "Gender",
                "MinRank" => "Minimum Star Rank",
                "MaxRank" => "Maximum Star Rank",
                _ => propertyName,
            };
        }

        return IsDynamaxAdventure(componentType) ? propertyName switch
        {
            "IndexNum" => "Adventure Index",
            "SpeciesID" => "Species",
            "Form" => "Form",
            "Level" => "Level",
            "BallID" => "Ball",
            "AbilitySlot" => "Ability Roll",
            "Gigantamax" => "Gigantamax",
            "GameVersion" => "Game Version",
            "ShinyRoll" => "Shiny Roll",
            "MoveSlot1" => "Move 1",
            "MoveSlot2" => "Move 2",
            "MoveSlot3" => "Move 3",
            "MoveSlot4" => "Move 4",
            "GuaranteedPerfectIVs" => "Guaranteed Perfect IVs",
            "IVATK" => "Attack IV",
            "IVDEF" => "Defense IV",
            "IVSPA" => "Sp. Atk IV",
            "IVSPD" => "Sp. Def IV",
            "IVSPE" => "Speed IV",
            "IsSingleCapture" => "Single-Capture Pokemon",
            "SingleCaptureFlagBlock" => "Single-Capture Flag Block",
            "IsStoryProgressGated" => "Requires Story Progress",
            "OTGender" => "OT Gender Value",
            "UiMessageID" => "UI Message Hash",
            _ => propertyName,
        } : propertyName;
    }

    public static string GetDescription(Type componentType, string propertyName)
    {
        if (!IsRaidType(componentType))
            return string.Empty;

        if (IsEncounterNestTable(componentType))
        {
            return propertyName switch
            {
                "TableID" => "Hash/ID for this den encounter table. Reward and placement data can reference this value.",
                "Version" => "Game version this den table applies to.",
                "EntryCount" => "Number of raid slots inside this den table.",
                "Entries" => "Pokemon raid slots available in this den table.",
                _ => string.Empty,
            };
        }

        if (IsEncounterNest(componentType))
        {
            return propertyName switch
            {
                "EntryIndex" => "Slot number inside this den table.",
                "SpeciesID" => "Pokemon species used by this raid slot.",
                "Form" => "Form index for the selected species. Zero is the default form.",
                "LevelTableID" => "Hash/ID of the level scaling table used by this raid slot.",
                "AbilityRoll" => "Allowed ability roll for the raid Pokemon: first ability, second ability, hidden ability, first/second only, or any.",
                "IsGigantamax" => "Allows this raid Pokemon to use its Gigantamax form when the species supports it.",
                "DropTableID" => "Reward table used for normal raid drops.",
                "BonusTableID" => "Reward table used for bonus raid drops.",
                "Star1Probability" => "Selection chance for this slot when the den rolls a 1-star raid.",
                "Star2Probability" => "Selection chance for this slot when the den rolls a 2-star raid.",
                "Star3Probability" => "Selection chance for this slot when the den rolls a 3-star raid.",
                "Star4Probability" => "Selection chance for this slot when the den rolls a 4-star raid.",
                "Star5Probability" => "Selection chance for this slot when the den rolls a 5-star raid.",
                "FlawlessIVs" => "Guaranteed perfect IV count for this raid slot.",
                "GenderType" => "Fixed gender behavior for this raid slot.",
                "MinRank" => "Lowest nonzero star rank for this raid slot. Derived from the star chances.",
                "MaxRank" => "Highest nonzero star rank for this raid slot. Derived from the star chances.",
                _ => string.Empty,
            };
        }

        return IsDynamaxAdventure(componentType) ? propertyName switch
        {
            "IndexNum" => "Internal Dynamax Adventures entry number.",
            "SpeciesID" => "Pokemon species used by this Dynamax Adventures rental/legendary encounter.",
            "Form" => "Form index for the selected species. Zero is the default form.",
            "Level" => "Encounter level.",
            "BallID" => "Ball shown/stored for this Dynamax Adventures Pokemon. Base data uses Poke Ball for all entries.",
            "AbilitySlot" => "Ability behavior. Base data uses Default, Ability 1, and Ability 2.",
            "Gigantamax" => "Whether this encounter uses its normal Dynamax-capable form or Gigantamax form.",
            "GameVersion" => "Version restriction for this entry. Both means Sword and Shield.",
            "ShinyRoll" => "Observed as Enabled for all base Dynamax Adventures entries. Other values are not fully confirmed.",
            "MoveSlot1" => "First move used by this encounter.",
            "MoveSlot2" => "Second move used by this encounter.",
            "MoveSlot3" => "Third move used by this encounter.",
            "MoveSlot4" => "Fourth move used by this encounter.",
            "GuaranteedPerfectIVs" => "Number of guaranteed perfect IVs. Base Dynamax Adventures entries store this as -5 in the HP IV field.",
            "IVATK" or "IVDEF" or "IVSPA" or "IVSPD" or "IVSPE" => "Individual IV override. -1 means random; 0-31 forces that IV.",
            "IsSingleCapture" => "True for one-time legendary/Ultra Beast captures.",
            "SingleCaptureFlagBlock" => "Save block hash used to remember single-capture completion.",
            "IsStoryProgressGated" => "True for entries unlocked after enough Dynamax Adventures story progress.",
            "OTGender" => "Raw OT gender flag. Base data uses 1 for all entries.",
            "UiMessageID" => "Message hash used by the UI for this entry.",
            _ => string.Empty,
        } : string.Empty;
    }

    public static string GetCategory(Type componentType, string propertyName)
    {
        if (!IsRaidType(componentType))
            return string.Empty;

        if (IsEncounterNestTable(componentType))
            return propertyName is "Entries" ? "Raid Slots" : "Table";

        if (IsEncounterNest(componentType))
        {
            return propertyName switch
            {
                "EntryIndex" or "SpeciesID" or "Form" or "GenderType" or "IsGigantamax" or "AbilityRoll" or "FlawlessIVs" => "Pokemon",
                "Star1Probability" or "Star2Probability" or "Star3Probability" or "Star4Probability" or "Star5Probability" or "MinRank" or "MaxRank" => "Star Chances",
                "LevelTableID" or "DropTableID" or "BonusTableID" => "Linked Tables",
                _ => "Raw / Unknown",
            };
        }

        if (IsDynamaxAdventure(componentType))
        {
            return propertyName switch
            {
                "IndexNum" or "SpeciesID" or "Form" or "Level" or "BallID" => "Pokemon",
                "MoveSlot1" or "MoveSlot2" or "MoveSlot3" or "MoveSlot4" => "Moves",
                "AbilitySlot" or "Gigantamax" or "GameVersion" or "ShinyRoll" or "IsStoryProgressGated" => "Encounter Rules",
                "IsSingleCapture" or "SingleCaptureFlagBlock" or "UiMessageID" or "OTGender" => "Flags / References",
                "GuaranteedPerfectIVs" or "IVATK" or "IVDEF" or "IVSPA" or "IVSPD" or "IVSPE" => "IVs",
                _ => "Raw / Unknown",
            };
        }

        return string.Empty;
    }

    public static TypeConverter? GetConverter(Type componentType, string propertyName)
    {
        if (!IsRaidType(componentType))
            return null;

        return propertyName switch
        {
            "TableID" or "LevelTableID" or "DropTableID" or "BonusTableID" => new RaidTableIdConverter(propertyName),
            "SpeciesID" when IsDynamaxAdventure(componentType) => new RaidNamedEnumConverter<Species>(DynamaxAdventureSpeciesNames),
            "MoveSlot1" or "MoveSlot2" or "MoveSlot3" or "MoveSlot4" when IsDynamaxAdventure(componentType) => new RaidNamedEnumConverter<Move>(DynamaxAdventureMoveNames),
            "IVATK" or "IVDEF" or "IVSPA" or "IVSPD" or "IVSPE" when IsDynamaxAdventure(componentType) => new RaidIVConverter(),
            "SingleCaptureFlagBlock" or "UiMessageID" when IsDynamaxAdventure(componentType) => new RaidUInt64HexConverter(),
            _ => null,
        };
    }

    public static string GetEncounterTableUsage(object? component)
    {
        return component is EncounterNestTable table
            ? GetEncounterTableUsage(table.TableID)
            : "Unable to read encounter table placement usage.";
    }

    public static string GetEncounterTableUsage(ulong tableID)
    {
        return EncounterTableUsageLabels.TryGetValue(tableID, out var label)
            ? label
            : "No base-game nest placement reference found. This table may be unused, event-only, or referenced by data outside placement.gfpak.";
    }

    public static string GetListItemDisplayName(Type componentType, string listName, int index)
    {
        if (!IsRaidType(componentType))
            return $"[{index}]";

        return listName switch
        {
            "Entries" => $"Slot {index + 1:00}",
            _ => $"[{index}]",
        };
    }

    public static IReadOnlyList<ulong> GetTableIDs(string propertyName)
    {
        return propertyName switch
        {
            "TableID" => EncounterTableLabels.Keys.ToArray(),
            "LevelTableID" => LevelTableLabels.Keys.ToArray(),
            "DropTableID" => DropRewardTableLabels.Keys.ToArray(),
            "BonusTableID" => BonusRewardTableLabels.Keys.ToArray(),
            _ => [],
        };
    }

    public static string GetTableLabel(string propertyName, ulong tableID)
    {
        var labels = propertyName switch
        {
            "TableID" => EncounterTableLabels,
            "LevelTableID" => LevelTableLabels,
            "DropTableID" => DropRewardTableLabels,
            "BonusTableID" => BonusRewardTableLabels,
            _ => null,
        };

        return labels != null && labels.TryGetValue(tableID, out var label)
            ? $"{label} - {tableID} (0x{tableID:X16})"
            : $"{tableID} (0x{tableID:X16})";
    }

    private static void ConfigureTableLabels(Dictionary<ulong, string> labels, string prefix, IReadOnlyList<ulong> tableIDs)
    {
        labels.Clear();
        for (int i = 0; i < tableIDs.Count; i++)
            labels[tableIDs[i]] = $"{prefix} {i:000}";
    }

    private static string GetIndexedName(IReadOnlyList<string> names, int index)
    {
        if ((uint)index < (uint)names.Count && !string.IsNullOrWhiteSpace(names[index]))
            return $"{names[index]} ({index})";

        return index.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed class RaidPropertyDescriptor(PropertyDescriptor baseDescriptor) : PropertyDescriptor(baseDescriptor)
{
    public override bool CanResetValue(object component) => baseDescriptor.CanResetValue(component);
    public override Type ComponentType => baseDescriptor.ComponentType;
    public override string Category => RaidPropertyGridUtil.GetCategory(ComponentType, Name);
    public override TypeConverter Converter => RaidPropertyGridUtil.GetConverter(ComponentType, Name) ?? baseDescriptor.Converter;
    public override string Description => RaidPropertyGridUtil.GetDescription(ComponentType, Name);
    public override string DisplayName => RaidPropertyGridUtil.GetDisplayName(ComponentType, Name);
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

public sealed class RaidPlacementUsagePropertyDescriptor()
    : PropertyDescriptor("PlacementUsage", null)
{
    public override bool CanResetValue(object component) => false;
    public override Type ComponentType => typeof(object);
    public override string Category => "Table";
    public override string Description => "Base-game placement nests that reference this encounter table as their common or rare raid table.";
    public override string DisplayName => "Placement Usage";
    public override object? GetValue(object? component) => RaidPropertyGridUtil.GetEncounterTableUsage(component);
    public override bool IsReadOnly => true;
    public override Type PropertyType => typeof(string);
    public override void ResetValue(object component) { }
    public override void SetValue(object? component, object? value) { }
    public override bool ShouldSerializeValue(object component) => false;
}

public sealed class RaidTableIdConverter(string propertyName) : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text && TryParseTableID(text, out var tableID))
            return tableID;

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is ulong tableID)
            return RaidPropertyGridUtil.GetTableLabel(propertyName, tableID);

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        return new StandardValuesCollection(RaidPropertyGridUtil.GetTableIDs(propertyName).ToArray());
    }

    private static bool TryParseTableID(string text, out ulong tableID)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            tableID = 0;
            return false;
        }

        var hexIndex = text.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
        if (hexIndex >= 0)
        {
            var hex = new string(text[(hexIndex + 2)..].TakeWhile(Uri.IsHexDigit).ToArray());
            if (hex.Length != 0 && ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out tableID))
                return true;
        }

        var decimalDigits = new string(text.SkipWhile(z => !char.IsDigit(z)).TakeWhile(char.IsDigit).ToArray());
        return ulong.TryParse(decimalDigits, NumberStyles.Integer, CultureInfo.InvariantCulture, out tableID);
    }
}

public sealed class RaidNamedEnumConverter<TEnum>(IReadOnlyList<string> names) : EnumConverter(typeof(TEnum)) where TEnum : struct, Enum
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text && TryParseNamedValue(text, out var index))
            return Enum.ToObject(typeof(TEnum), index);

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is TEnum typed)
            return Format(Convert.ToInt32(typed, CultureInfo.InvariantCulture));

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var values = Enumerable.Range(0, names.Count)
            .Select(index => Enum.ToObject(typeof(TEnum), index))
            .ToArray();
        return new StandardValuesCollection(values);
    }

    private string Format(int index)
    {
        if ((uint)index < (uint)names.Count && !string.IsNullOrWhiteSpace(names[index]))
            return $"{names[index]} ({index})";

        return index.ToString(CultureInfo.InvariantCulture);
    }

    private bool TryParseNamedValue(string text, out int index)
    {
        text = text.Trim();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            return true;

        var open = text.LastIndexOf('(');
        var close = text.LastIndexOf(')');
        if (open >= 0 && close > open && int.TryParse(text[(open + 1)..close], NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            return true;

        var nameOnly = open > 0 ? text[..open].Trim() : text;
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], nameOnly, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                return true;
            }
        }

        if (Enum.TryParse<TEnum>(text, true, out var enumValue))
        {
            index = Convert.ToInt32(enumValue, CultureInfo.InvariantCulture);
            return true;
        }

        index = 0;
        return false;
    }
}

public sealed class RaidIVConverter : SByteConverter
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
            return iv == -1 ? "Random (-1)" : iv.ToString(CultureInfo.InvariantCulture);

        return base.ConvertTo(context, culture, value, destinationType);
    }
}

public sealed class RaidUInt64HexConverter : UInt64Converter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            var hexIndex = text.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
            if (hexIndex >= 0)
            {
                var hex = new string(text[(hexIndex + 2)..].TakeWhile(Uri.IsHexDigit).ToArray());
                if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValue))
                    return hexValue;
            }

            var digits = new string(text.SkipWhile(z => !char.IsDigit(z)).TakeWhile(char.IsDigit).ToArray());
            if (ulong.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimalValue))
                return decimalValue;
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is ulong hash)
            return $"{hash} (0x{hash:X16})";

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
