using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Text;
using pkNX.Structures;
using pkNX.Structures.FlatBuffers.SWSH;

namespace pkNX.WinForms;

public static class MovePropertyGridUtil
{
    private static string[] MoveNames = [];

    public static void Configure(IReadOnlyList<string> moveNames)
    {
        MoveNames = moveNames.ToArray();
    }

    public static bool IsMoveType(Type type) => typeof(Waza).IsAssignableFrom(type) || type.FullName == "pkNX.Structures.FlatBuffers.SWSH.Waza";

    public static bool ShouldHide(Type componentType, string propertyName)
    {
        return IsMoveType(componentType) && propertyName is
            "MoveID" or
            "Type" or
            "Category" or
            "Inflict" or
            "RawInflictCount" or
            "RawHealing" or
            "RawTarget" or
            "Stat1" or
            "Stat2" or
            "Stat3";
    }

    public static bool IsReadOnly(Type componentType, string propertyName)
    {
        return IsMoveType(componentType) && propertyName == "MoveIdentifier";
    }

    public static string GetDisplayName(Type componentType, string propertyName)
    {
        if (!IsMoveType(componentType))
            return propertyName;

        return propertyName switch
        {
            "MoveIdentifier" => "Move",
            "MoveID" => "Move ID",
            "CanUseMove" => "Enabled",
            "MoveType" => "Type",
            "DamageCategory" => "Category",
            "CritStage" => "Critical Stage",
            "HitMin" => "Minimum Hits",
            "HitMax" => "Maximum Hits",
            "InflictedStatus" => "Inflicted Status",
            "InflictPercent" => "Inflict Chance",
            "InflictCount" => "Inflict Duration",
            "TurnMin" => "Minimum Turns",
            "TurnMax" => "Maximum Turns",
            "RawHealing" => "Healing",
            "Stat1ID" => "Stat Change 1: Stat",
            "Stat2ID" => "Stat Change 2: Stat",
            "Stat3ID" => "Stat Change 3: Stat",
            "Stat1Stage" => "Stat Change 1: Stage Delta",
            "Stat2Stage" => "Stat Change 2: Stage Delta",
            "Stat3Stage" => "Stat Change 3: Stage Delta",
            "Stat1Percent" => "Stat Change 1: Chance (%)",
            "Stat2Percent" => "Stat Change 2: Chance (%)",
            "Stat3Percent" => "Stat Change 3: Chance (%)",
            "GigantamaxPower" => "Max Move Power",
            "FlagMakesContact" => "Makes Contact",
            "FlagCharge" => "Charge Turn",
            "FlagRecharge" => "Recharge Turn",
            "FlagProtect" => "Blocked By Protect",
            "FlagReflectable" => "Reflectable",
            "FlagSnatch" => "Snatchable",
            "FlagMirror" => "Mirror Move",
            "FlagPunch" => "Punch Move",
            "FlagSound" => "Sound Move",
            "FlagGravity" => "Fails Under Gravity",
            "FlagDefrost" => "Thaws User",
            "FlagDistanceTriple" => "Triple Battle Distance",
            "FlagHeal" => "Heal Move",
            "FlagIgnoreSubstitute" => "Ignores Substitute",
            "FlagFailSkyBattle" => "Fails In Sky Battle",
            "FlagAnimateAlly" => "Animate Ally",
            "FlagDance" => "Dance Move",
            "FlagMetronome" => "Callable By Metronome",
            _ => SplitGeneratedName(propertyName),
        };
    }

    public static string GetDescription(Type componentType, string propertyName)
    {
        if (!IsMoveType(componentType))
            return string.Empty;

        return propertyName switch
        {
            "MoveIdentifier" => "Move represented by this file entry. This is shown for reference and should not usually be edited.",
            "CanUseMove" => "Whether this move is enabled/usable in the move data.",
            "MoveType" => "Elemental type used for damage, STAB, effectiveness, and type-based interactions.",
            "DamageCategory" => "Move category: status, physical, or special.",
            "Power" => "Base power. Zero is used for status moves and special formula moves.",
            "Accuracy" => "Base accuracy. Zero commonly means the move does not use a normal accuracy check.",
            "PP" => "Base PP before PP Up/PP Max modifiers.",
            "Priority" => "Turn-order priority modifier.",
            "Target" => "Battle target behavior for the move.",
            "InflictedStatus" => "Primary status, volatile status, or special condition inflicted by this move.",
            "InflictPercent" => "Chance to inflict the selected status/effect.",
            "InflictCount" => "How long the inflicted status/effect lasts.",
            "Healing" => "Healing behavior. Named values are common fractional heals; other raw values may represent fixed or special behavior.",
            "Recoil" => "Recoil or drain-style percentage. Negative and positive values can have different behavior depending on the effect sequence.",
            "EffectSequence" => "Raw battle effect script/sequence ID. This controls special behavior and is not fully mapped yet.",
            "Quality" => "Raw quality/AI value. Meaning is not fully mapped yet.",
            "Stat1ID" => "First stat-change slot. Select the stat this move can raise or lower. None disables this slot unless the battle effect sequence handles it specially.",
            "Stat2ID" => "Second stat-change slot. Use this when a move changes more than one stat, such as Ancient Power-style effects.",
            "Stat3ID" => "Third stat-change slot. Use this when a move changes up to three stats from the same move effect.",
            "Stat1Stage" => "Stage delta for stat-change slot 1. Positive values raise the stat; negative values lower it. Example: 2 means +2 stages, -1 means -1 stage.",
            "Stat2Stage" => "Stage delta for stat-change slot 2. Positive values raise the stat; negative values lower it.",
            "Stat3Stage" => "Stage delta for stat-change slot 3. Positive values raise the stat; negative values lower it.",
            "Stat1Percent" => "Percent chance for stat-change slot 1 to apply. Some special effect sequences may ignore or override this value.",
            "Stat2Percent" => "Percent chance for stat-change slot 2 to apply. Some special effect sequences may ignore or override this value.",
            "Stat3Percent" => "Percent chance for stat-change slot 3 to apply. Some special effect sequences may ignore or override this value.",
            "GigantamaxPower" => "Power used when this move is converted into a Max Move.",
            "FlagMakesContact" => "True if the move makes physical contact. Contact can trigger abilities, items, and other contact-based effects.",
            "FlagCharge" => "True if the move uses a charge/preparation turn before the main hit. The exact behavior still depends on the effect sequence.",
            "FlagRecharge" => "True if the user must recharge after the move, as with Hyper Beam-style moves.",
            "FlagProtect" => "True if Protect, Detect, and similar protection effects can block this move.",
            "FlagReflectable" => "True if reflection effects such as Magic Coat/Magic Bounce-style behavior can reflect the move.",
            "FlagSnatch" => "True if Snatch-style behavior can steal or redirect this move's effect.",
            "FlagMirror" => "True if Mirror Move-style behavior can call this move back.",
            "FlagPunch" => "True if the move is treated as a punching move, affecting interactions such as Iron Fist.",
            "FlagSound" => "True if the move is sound-based, affecting interactions such as Soundproof and sound-related modifiers.",
            "FlagGravity" => "True if the move fails or is restricted while Gravity is active.",
            "FlagDefrost" => "True if using this move thaws the user when frozen.",
            "FlagDistanceTriple" => "Legacy triple-battle range flag. True means the move can interact with distant targets in triple-battle layouts.",
            "FlagHeal" => "True if the move is treated as a healing move for battle rules and move interactions.",
            "FlagIgnoreSubstitute" => "True if the move bypasses Substitute when applying its effect.",
            "FlagFailSkyBattle" => "Legacy Sky Battle restriction flag. True means the move was marked unusable in Sky Battles.",
            "FlagAnimateAlly" => "Animation/targeting helper for moves that involve an ally. Usually leave this unchanged unless matching another known move.",
            "FlagDance" => "True if the move is a dance move, affecting Dancer-style interactions.",
            "FlagMetronome" => "True if this move is eligible to be called by Metronome-style random move selection.",
            _ when propertyName.StartsWith("Flag", StringComparison.Ordinal) => "Battle behavior flag used by move interactions. Meaning is not fully mapped yet.",
            _ => string.Empty,
        };
    }

    public static string GetCategory(Type componentType, string propertyName)
    {
        if (!IsMoveType(componentType))
            return string.Empty;

        return propertyName switch
        {
            "MoveIdentifier" or "MoveID" or "CanUseMove" or "Version" => "Identity",
            "MoveType" or "DamageCategory" or "Power" or "Accuracy" or "PP" or "Priority" or "CritStage" or "GigantamaxPower" => "Core Stats",
            "Target" or "HitMin" or "HitMax" or "TurnMin" or "TurnMax" => "Targeting / Timing",
            "InflictedStatus" or "InflictPercent" or "InflictCount" or "Flinch" or "Healing" or "Recoil" => "Secondary Effects",
            "Stat1ID" or "Stat2ID" or "Stat3ID" or "Stat1Stage" or "Stat2Stage" or "Stat3Stage" or "Stat1Percent" or "Stat2Percent" or "Stat3Percent" => "Stat Change Effects",
            _ when propertyName.StartsWith("Flag", StringComparison.Ordinal) => "Flags",
            _ => "Raw / Unknown",
        };
    }

    public static TypeConverter? GetConverter(Type componentType, string propertyName)
    {
        if (!IsMoveType(componentType))
            return null;

        return propertyName switch
        {
            "MoveIdentifier" or "MoveID" => new MoveNameValueConverter(),
            "MoveType" => new MoveEnumConverter<Types>(),
            "DamageCategory" => new MoveEnumConverter<MoveDamageCategory>(),
            "Target" => new MoveEnumConverter<MoveTarget>(),
            "InflictedStatus" => new MoveEnumConverter<MoveInflict>(),
            "InflictCount" => new MoveEnumConverter<MoveInflictDuration>(),
            "Healing" => new MoveEnumConverter<Heal>(),
            "Stat1ID" or "Stat2ID" or "Stat3ID" => new MoveEnumConverter<MoveStat>(),
            _ => null,
        };
    }

    internal static string GetMoveName(int index)
    {
        return (uint)index < (uint)MoveNames.Length && !string.IsNullOrWhiteSpace(MoveNames[index])
            ? $"{MoveNames[index]} ({index})"
            : index.ToString(CultureInfo.InvariantCulture);
    }

    internal static int GetMoveCount() => MoveNames.Length;

    internal static bool TryGetMoveIndex(string text, out int index)
    {
        text = text.Trim();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            return true;

        var open = text.LastIndexOf('(');
        var close = text.LastIndexOf(')');
        if (open >= 0 && close > open && int.TryParse(text[(open + 1)..close], NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            return true;

        var nameOnly = open > 0 ? text[..open].Trim() : text;
        index = Array.FindIndex(MoveNames, z => string.Equals(z, nameOnly, StringComparison.OrdinalIgnoreCase));
        return index >= 0;
    }

    internal static string SplitGeneratedName(string name)
    {
        if (name.StartsWith("Flag_", StringComparison.Ordinal))
            name = "Flag" + name[5..];

        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '_')
            {
                builder.Append(' ');
                continue;
            }

            if (i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1]))
                builder.Append(' ');

            builder.Append(c);
        }

        return builder.ToString();
    }
}

public sealed class MovePropertyDescriptor(PropertyDescriptor baseDescriptor) : PropertyDescriptor(baseDescriptor)
{
    public override bool CanResetValue(object component) => baseDescriptor.CanResetValue(component);
    public override Type ComponentType => baseDescriptor.ComponentType;
    public override string Category => MovePropertyGridUtil.GetCategory(ComponentType, Name);
    public override TypeConverter Converter => MovePropertyGridUtil.GetConverter(ComponentType, Name) ?? baseDescriptor.Converter;
    public override string Description => MovePropertyGridUtil.GetDescription(ComponentType, Name);
    public override string DisplayName => MovePropertyGridUtil.GetDisplayName(ComponentType, Name);
    public override object? GetValue(object? component) => baseDescriptor.GetValue(component);
    public override bool IsReadOnly => baseDescriptor.IsReadOnly || MovePropertyGridUtil.IsReadOnly(ComponentType, Name);
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

public sealed class MoveNameValueConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text && MovePropertyGridUtil.TryGetMoveIndex(text, out var index))
            return ConvertIndex(index, context?.PropertyDescriptor?.PropertyType ?? typeof(uint));

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value != null)
            return MovePropertyGridUtil.GetMoveName(Convert.ToInt32(value, CultureInfo.InvariantCulture));

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var propertyType = context?.PropertyDescriptor?.PropertyType ?? typeof(uint);
        var values = Enumerable.Range(0, MovePropertyGridUtil.GetMoveCount())
            .Select(index => ConvertIndex(index, propertyType))
            .ToArray();
        return new StandardValuesCollection(values);
    }

    private static object ConvertIndex(int index, Type propertyType)
    {
        return propertyType.IsEnum
            ? Enum.ToObject(propertyType, index)
            : Convert.ChangeType(index, propertyType, CultureInfo.InvariantCulture);
    }
}

public sealed class MoveEnumConverter<TEnum> : EnumConverter where TEnum : struct, Enum
{
    public MoveEnumConverter() : base(typeof(TEnum)) { }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text && TryParseValue(text, out var result))
            return result;

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is TEnum typed)
            return Format(typed);

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        return new StandardValuesCollection(Enum.GetValues<TEnum>().Cast<object>().ToArray());
    }

    private static string Format(TEnum value)
    {
        var numeric = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        var name = Enum.GetName(value);
        return name == null
            ? numeric.ToString(CultureInfo.InvariantCulture)
            : $"{MovePropertyGridUtil.SplitGeneratedName(name)} ({numeric})";
    }

    private static bool TryParseValue(string text, out TEnum result)
    {
        text = text.Trim();
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            result = (TEnum)Enum.ToObject(typeof(TEnum), numeric);
            return true;
        }

        var open = text.LastIndexOf('(');
        var close = text.LastIndexOf(')');
        if (open >= 0 && close > open && long.TryParse(text[(open + 1)..close], NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
        {
            result = (TEnum)Enum.ToObject(typeof(TEnum), numeric);
            return true;
        }

        var nameOnly = open > 0 ? text[..open].Trim() : text;
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var name = Enum.GetName(value);
            if (name == null)
                continue;

            if (string.Equals(name, nameOnly, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(MovePropertyGridUtil.SplitGeneratedName(name), nameOnly, StringComparison.OrdinalIgnoreCase))
            {
                result = value;
                return true;
            }
        }

        result = default;
        return false;
    }
}
