using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using pkNX.Structures;
using SWSHSymbolBehave = pkNX.Structures.FlatBuffers.SWSH.SymbolBehave;

namespace pkNX.WinForms;

public static class SymbolBehaviorPropertyGridUtil
{
    private static readonly string[] UnusedDefaultFields =
    [
        "Field08",
        "Field12",
        "Field14",
        "Field15",
        "Field28",
        "Field30",
        "Field33",
        "Field34",
        "Field35",
        "Field36",
        "Field42",
        "Field43",
    ];

    private static string[] SpeciesNames = [];
    private static string[] BehaviorNames = [];

    public static void Configure(IReadOnlyList<string> speciesNames, IReadOnlyList<string> behaviorNames)
    {
        SpeciesNames = speciesNames.ToArray();
        BehaviorNames = behaviorNames
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsSymbolBehaviorType(Type type) => typeof(SWSHSymbolBehave).IsAssignableFrom(type) || type.FullName == "pkNX.Structures.FlatBuffers.SWSH.SymbolBehave";

    public static string GetSymbolBehaviorName(SWSHSymbolBehave behavior, int index)
    {
        var species = GetIndexedName(SpeciesNames, behavior.SpeciesID, includeID: false);
        var form = behavior.Form == 0 ? string.Empty : $"-{behavior.Form}";
        var mode = string.IsNullOrWhiteSpace(behavior.Behavior) ? "No Behavior" : behavior.Behavior;
        return $"{index:000} {species}{form} | {mode}";
    }

    public static bool ShouldHide(Type componentType, string propertyName)
    {
        return IsSymbolBehaviorType(componentType) &&
            (propertyName is "Species" or "SpeciesNameJPN" || UnusedDefaultFields.Contains(propertyName));
    }

    public static string GetDisplayName(Type componentType, string propertyName)
    {
        if (!IsSymbolBehaviorType(componentType))
            return propertyName;

        return propertyName switch
        {
            "SpeciesID" => "Species",
            "Form" => "Form",
            "Behavior" => "Behavior",
            "ModelPart" => "Model Anchor",
            "HitboxRadius" => "Hitbox Radius",
            "GrassShakeRadius" => "Grass Shake Radius",
            "Hash1" => "Internal Hash 1",
            "Hash2" => "Internal Hash 2",
            _ when IsParameterField(propertyName) => $"Behavior Parameter {propertyName[5..]}",
            _ => MovePropertyGridUtil.SplitGeneratedName(propertyName),
        };
    }

    public static string GetDescription(Type componentType, string propertyName)
    {
        if (!IsSymbolBehaviorType(componentType))
            return string.Empty;

        return propertyName switch
        {
            "SpeciesID" => "Pokemon species this symbol behavior entry applies to.",
            "Form" => "Form index for the selected species. Zero is the default form.",
            "Behavior" => "Primary field AI profile. This is the value most likely to control whether a wild Pokemon notices, approaches, chases, stares at, dashes at, disappears from, or flees from the player.",
            "ModelPart" => "Named model part used as the interaction/collision anchor. Base data commonly uses values such as Waist.",
            "HitboxRadius" => "Radius used by the symbol encounter's collision or interaction hitbox.",
            "GrassShakeRadius" => "Radius used for grass-shake behavior. Zero disables that radius for entries that do not use it.",
            "Hash1" => "Unresolved internal symbol behavior hash/reference. Kept editable for advanced testing.",
            "Hash2" => "Unresolved internal symbol behavior hash/reference. The common FNV empty hash is displayed as None.",
            _ when IsParameterField(propertyName) => "Unmapped symbol AI tuning value. Compare against a Pokemon with the behavior you want before changing this.",
            _ => string.Empty,
        };
    }

    public static string GetCategory(Type componentType, string propertyName)
    {
        if (!IsSymbolBehaviorType(componentType))
            return string.Empty;

        return propertyName switch
        {
            "SpeciesID" or "Form" => "Identity",
            "Behavior" => "Behavior",
            "ModelPart" or "HitboxRadius" or "GrassShakeRadius" => "Collision / Range",
            "Hash1" or "Hash2" => "Internal References",
            _ when IsParameterField(propertyName) => "Behavior Tuning",
            _ => "Raw / Unknown",
        };
    }

    public static TypeConverter? GetConverter(Type componentType, string propertyName)
    {
        if (!IsSymbolBehaviorType(componentType))
            return null;

        return propertyName switch
        {
            "SpeciesID" => new SymbolBehaviorSpeciesConverter(),
            "Behavior" => new SymbolBehaviorModeConverter(BehaviorNames),
            "Hash1" or "Hash2" => new SymbolBehaviorHashConverter(),
            _ => null,
        };
    }

    private static bool IsParameterField(string propertyName)
    {
        return propertyName.StartsWith("Field", StringComparison.Ordinal) &&
            propertyName.Length == 7 &&
            int.TryParse(propertyName[5..], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static string GetIndexedName(IReadOnlyList<string> names, int index, bool includeID)
    {
        if ((uint)index < (uint)names.Count && !string.IsNullOrWhiteSpace(names[index]))
            return includeID ? $"{names[index]} ({index})" : names[index];

        return index.ToString(CultureInfo.InvariantCulture);
    }

    internal static string GetSpeciesName(int speciesID)
        => GetIndexedName(SpeciesNames, speciesID, includeID: true);

    internal static int GetSpeciesCount() => SpeciesNames.Length;

    internal static bool TryGetSpeciesID(string text, out int speciesID)
    {
        text = text.Trim();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out speciesID))
            return true;

        var open = text.LastIndexOf('(');
        var close = text.LastIndexOf(')');
        if (open >= 0 && close > open && int.TryParse(text[(open + 1)..close], NumberStyles.Integer, CultureInfo.InvariantCulture, out speciesID))
            return true;

        var nameOnly = open > 0 ? text[..open].Trim() : text;
        speciesID = Array.FindIndex(SpeciesNames, z => string.Equals(z, nameOnly, StringComparison.OrdinalIgnoreCase));
        return speciesID >= 0;
    }

    internal static string GetBehaviorLabel(string behavior)
    {
        return behavior switch
        {
            "Anawohoru" => "Anawohoru - Diglett burrow / pop-up behavior",
            "Appeal" => "Appeal - notices player / attention behavior",
            "Approach" => "Approach - moves toward or chases player",
            "Common" => "Common - standard wild movement behavior",
            "Escape" => "Escape - flees from the player",
            "Haneru" => "Haneru - Magikarp splash / flop behavior",
            "Hindrance" => "Hindrance - Obstagoon blocking behavior",
            "Homing" => "Homing - charging pursuit behavior",
            "JumpWater" => "JumpWater - water jump / surface leap behavior",
            "Maggyo" => "Maggyo - Stunfisk trap behavior",
            "Massuguma" => "Massuguma - Linoone dash behavior",
            "Warp" => "Warp - teleport away behavior",
            "WaterDash" => "WaterDash - Sharpedo-style water dash",
            "Ziguzaguma" => "Ziguzaguma - Zigzagoon zigzag movement",
            _ => behavior,
        };
    }

    internal static bool TryGetBehaviorValue(string text, out string behavior)
    {
        text = text.Trim();
        var separator = text.IndexOf(" - ", StringComparison.Ordinal);
        behavior = separator > 0 ? text[..separator].Trim() : text;
        return behavior.Length != 0;
    }
}

public sealed class SymbolBehaviorPropertyDescriptor(PropertyDescriptor baseDescriptor) : PropertyDescriptor(baseDescriptor)
{
    public override bool CanResetValue(object component) => baseDescriptor.CanResetValue(component);
    public override Type ComponentType => baseDescriptor.ComponentType;
    public override string Category => SymbolBehaviorPropertyGridUtil.GetCategory(ComponentType, Name);
    public override TypeConverter Converter => SymbolBehaviorPropertyGridUtil.GetConverter(ComponentType, Name) ?? baseDescriptor.Converter;
    public override string Description => SymbolBehaviorPropertyGridUtil.GetDescription(ComponentType, Name);
    public override string DisplayName => SymbolBehaviorPropertyGridUtil.GetDisplayName(ComponentType, Name);
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

public sealed class SymbolBehaviorSpeciesConverter : Int32Converter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text && SymbolBehaviorPropertyGridUtil.TryGetSpeciesID(text, out var speciesID))
            return speciesID;

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is int speciesID)
            return SymbolBehaviorPropertyGridUtil.GetSpeciesName(speciesID);

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var values = Enumerable.Range(0, SymbolBehaviorPropertyGridUtil.GetSpeciesCount()).ToArray();
        return new StandardValuesCollection(values);
    }
}

public sealed class SymbolBehaviorModeConverter(IReadOnlyList<string> behaviors) : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        return new StandardValuesCollection(behaviors.Cast<object>().ToArray());
    }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text && SymbolBehaviorPropertyGridUtil.TryGetBehaviorValue(text, out var behavior))
            return behavior;

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is string behavior)
            return SymbolBehaviorPropertyGridUtil.GetBehaviorLabel(behavior);

        return base.ConvertTo(context, culture, value, destinationType);
    }
}

public sealed class SymbolBehaviorHashConverter : UInt64Converter
{
    private const ulong FnvEmptyHash = 14695981039346656837;

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (text.StartsWith("None", StringComparison.OrdinalIgnoreCase))
                return FnvEmptyHash;

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
            return hash == FnvEmptyHash ? $"None (0x{hash:X16})" : $"{hash} (0x{hash:X16})";

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
