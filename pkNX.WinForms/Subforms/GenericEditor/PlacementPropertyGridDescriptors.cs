using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using pkNX.Structures.FlatBuffers.SWSH;

namespace pkNX.WinForms;

internal sealed class PlacementPropertyDescriptor(PropertyDescriptor baseDescriptor)
    : PropertyDescriptor(baseDescriptor)
{
    public override bool CanResetValue(object component) => baseDescriptor.CanResetValue(component);
    public override Type ComponentType => baseDescriptor.ComponentType;
    public override string DisplayName => PlacementPropertyGridUtil.GetDisplayName(ComponentType, Name, PropertyType);
    public override object? GetValue(object? component) => baseDescriptor.GetValue(component);
    public override bool IsReadOnly => baseDescriptor.IsReadOnly;
    public override Type PropertyType => baseDescriptor.PropertyType;
    public override void ResetValue(object component) => baseDescriptor.ResetValue(component);
    public override void SetValue(object? component, object? value) => baseDescriptor.SetValue(component, value);
    public override bool ShouldSerializeValue(object component) => baseDescriptor.ShouldSerializeValue(component);
    public override TypeConverter Converter => PlacementPropertyGridUtil.GetConverter(baseDescriptor) ?? baseDescriptor.Converter;
}

internal static class PlacementPropertyGridUtil
{
    public static bool IsPlacementType(Type type)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.FullName?.StartsWith("pkNX.Structures.FlatBuffers.SWSH.PlacementZone", StringComparison.Ordinal) == true)
                return true;
        }

        return false;
    }

    public static string GetDisplayName(Type componentType, string name, Type propertyType)
    {
        return name switch
        {
            "HashObjectName" => "Object",
            "ZoneID" => "Zone",
            "LocationX" => "X",
            "LocationY" => "Y",
            "LocationZ" => "Z",
            "RotationX" => "Rotation X",
            "RotationY" => "Rotation Y",
            "RotationZ" => "Rotation Z",
            "ScaleX" => "Scale X",
            "ScaleY" => "Scale Y",
            "ScaleZ" => "Scale Z",
            "NameModel" => "Model",
            "NameAnimation" => "Animation",
            "ParticleFile" => "Particle",
            "PlayName" => "Play Event",
            "StopName" => "Stop Event",
            "SignHash" => "Sign",
            "TriggerName" => "Trigger",
            "UnlockFlagHash" => "Unlock Flag",
            "PathName" => "Path",
            "ModelVariant" => "Variant",
            "EnableSpawns" => "Enable Spawn Flag",
            "Common" => "Common Den",
            "Rare" => "Rare Den",
            "NPCType1" => "NPC Type 1",
            "NPCType2" => "NPC Type 2",
            "PokeCenterAnchor" => "Pokemon Center Anchor",
            "AnimationIndexSecondary" => "Secondary Animation",
            _ when TryGetStaticObjectDisplayName(componentType, name, out var staticObjectDisplayName) => staticObjectDisplayName,
            _ when TryGetFieldItemDisplayName(componentType, name, out var fieldItemDisplayName) => fieldItemDisplayName,
            _ when TryGetTrainerDisplayName(componentType, name, out var trainerDisplayName) => trainerDisplayName,
            _ when TryGetSpeciesDisplayName(componentType, name, out var speciesDisplayName) => speciesDisplayName,
            _ when TryGetAdvancedTipDisplayName(componentType, name, out var advancedTipDisplayName) => advancedTipDisplayName,
            "Field00" when propertyType == typeof(PlacementZoneMetaTripleXYZ) => "Placement",
            "Field00" when IsHolderType(componentType) => "Entry",
            "Field00" when IsSpecificPlacementType(componentType, nameof(PlacementZoneAdvancedTip)) => "Tip",
            "Field01" when IsHolderType(componentType) => "Settings",
            "Field02" when IsSpecificPlacementType(componentType, nameof(PlacementZoneFieldItem)) => "Model",
            _ => SplitGeneratedName(name),
        };
    }

    public static TypeConverter? GetConverter(PropertyDescriptor descriptor)
    {
        if (descriptor.PropertyType == typeof(ulong))
            return new PlacementHashConverter(descriptor);

        if (descriptor.PropertyType == typeof(string) && IsCleanableString(descriptor))
            return new PlacementStringConverter(descriptor.Converter);

        if (IsPlacementType(descriptor.PropertyType))
            return new PlacementObjectConverter(descriptor.Converter);

        return null;
    }

    private static bool TryGetStaticObjectDisplayName(Type componentType, string name, out string displayName)
    {
        displayName = string.Empty;

        if (IsSpecificPlacementType(componentType, nameof(PlacementZoneStaticObject)))
        {
            displayName = name switch
            {
                "Identifier" => "Placement",
                "Field01" => "Flag",
                "Field03" => "Value",
                "Field04" => "Variant",
                "Field06" => "Box A",
                "Field07" => "Box B",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZoneStaticObjectSpawn)))
        {
            displayName = name switch
            {
                "SpawnID" => "Static Encounter",
                "Field02" => "Hash",
                "Field03" => "Value",
                "Field04" => "Settings",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZoneStaticObjectUnknown)))
        {
            displayName = name switch
            {
                "Field00" => "Type",
                "Field01" => "X",
                "Field02" => "Y",
                "Field03" => "Z",
                "Field04" => "Value",
                _ => string.Empty,
            };
        }

        return displayName.Length != 0;
    }

    private static bool TryGetFieldItemDisplayName(Type componentType, string name, out string displayName)
    {
        displayName = string.Empty;

        if (IsSpecificPlacementType(componentType, nameof(PlacementZoneFieldItem)))
        {
            displayName = name switch
            {
                "Field00" => "Placement",
                "Field02" => "Model",
                "Field09" => "Enabled",
                "Flags" => "Item Hashes",
                "Items" => "Fallback Item IDs",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZoneFieldItem_A)))
        {
            displayName = name switch
            {
                "Field00" => "Enabled",
                "Field01" => "Unused",
                _ => string.Empty,
            };
        }

        return displayName.Length != 0;
    }

    private static bool TryGetTrainerDisplayName(Type componentType, string name, out string displayName)
    {
        displayName = string.Empty;

        if (IsSpecificPlacementType(componentType, nameof(PlacementZoneTrainerHolder)))
        {
            displayName = name switch
            {
                "Field00" => "Trainer",
                "Field01" => "Range",
                "TrainerID" => "Trainer Battle",
                "Hash03" => "Event Hash",
                "MovementPath" => "Movement Path",
                "Unknown" => "Extra Data",
                "Field06" => "Behavior",
                "Field07" => "Behavior Settings",
                "Field08" => "Unused",
                "Field09" => "Unused",
                "Field10" => "Value 10",
                "Field11" => "Value 11",
                "Field12" => "Value 12",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F08)))
        {
            displayName = name switch
            {
                "Field00" => "Model / Placement",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F08_A)))
        {
            displayName = name switch
            {
                "Field00" => "Placement",
                "Hash01" => "Object Hash",
                "HashModel" => "Model",
                "Hash03" => "Extra Hash",
                "Field04" => "Range A",
                "Field06" => "Unused",
                "Hash06" => "Extra Hash",
                "Field07" => "Range B",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F08_ArrayEntry)))
        {
            displayName = name switch
            {
                "Field00" => "Type",
                "Field01" => "Value 01",
                "Field02" => "Value 02",
                "Field03" => "Value",
                "Field04" => "Flag",
                "Field05" => "Hash",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F08_Nine)))
        {
            displayName = name switch
            {
                "Field00" => "Flag 00",
                "Field01" => "Flag 01",
                "Field02" => "Flag 02",
                "Field03" => "Unused",
                "Hash04" => "Hash 04",
                "Field05" => "Flag 05",
                "Field06" => "Unused",
                "Hash07" => "Hash 07",
                "Field08" => "Unused",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F08_IntFloat)))
        {
            displayName = name switch
            {
                "Field00" => "Type",
                "Field01" => "X",
                "Field02" => "Y",
                "Field03" => "Z",
                "Field04" => "Value",
                _ => string.Empty,
            };
        }

        return displayName.Length != 0;
    }

    private static bool TryGetSpeciesDisplayName(Type componentType, string name, out string displayName)
    {
        displayName = string.Empty;

        if (IsSpecificPlacementType(componentType, nameof(PlacementZoneSpeciesHolder)))
        {
            displayName = name switch
            {
                "Field00" => "Entry",
                "Field01" => "Settings",
                "Unused2" => "Unused",
                "Field10" => "Extra Entries",
                "Field11" => "Value",
                "Field12" => "Behavior",
                "Field13" => "Spawn Type",
                "Field14" => "Group",
                "Num15" => "Flag",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F02)))
        {
            displayName = name switch
            {
                "Field00" => "Placement",
                "Field05" => "Unused 05",
                "Field06" => "Unused 06",
                "Field07" => "Unused 07",
                "Field08" => "Unused 08",
                "Field09" => "Empty Object 1",
                "Field10" => "Unused 10",
                "Field11" => "Empty Object 2",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F02_Field1)))
        {
            displayName = name switch
            {
                "Field00" => "Settings",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F02_Inner)))
        {
            displayName = name switch
            {
                "Field00" => "Placement",
                "Field04" => "Box A",
                "Num05" => "Flag",
                "Field07" => "Box B",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F02_IntFloat)))
        {
            displayName = name switch
            {
                "Field00" => "Type",
                "Field01" => "X",
                "Field02" => "Y",
                "Field03" => "Z",
                "Field04" => "Value",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F02_Nine)))
        {
            displayName = name switch
            {
                "Field00" => "Value 00",
                "Field01" => "Value 01",
                "Field02" => "Value 02",
                "Field03" => "Flag",
                "Field05" => "Value 05",
                "Field06" => "Unused",
                "Field09" => "Value 09",
                _ => string.Empty,
            };
        }

        return displayName.Length != 0;
    }

    private static bool TryGetAdvancedTipDisplayName(Type componentType, string name, out string displayName)
    {
        displayName = string.Empty;

        if (IsSpecificPlacementType(componentType, nameof(PlacementZoneAdvancedTipHolder)))
        {
            displayName = name switch
            {
                "Field00" => "Tip",
                "Field01" => "Unused 1",
                "Field02" => "Unused 2",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F14)))
        {
            displayName = name switch
            {
                "Field00" => "Placement",
                "Field03" => "Scale X",
                "Field04" => "Scale Y",
                "Field05" => "Model Variant",
                "Field06" => "Animation Variant",
                "Field07" => "Bounds Width",
                "Field08" => "Bounds Height",
                "Field09" => "Bounds Depth",
                "Field10" => "Unused",
                "Field11" => "Box A",
                "Field12" => "Unused",
                "Field13" => "Box B",
                "Field14" => "Range",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F14_B)))
        {
            displayName = name switch
            {
                "Field00" => "Type",
                "Field01" => "Unused 1",
                "Field02" => "Unused 2",
                "Field03" => "Offset X",
                "Field04" => "Unused 3",
                "Field05" => "Unused 4",
                "Field06" => "Offset Y",
                "Field07" => "Unused 5",
                "Field08" => "Width",
                "Field09" => "Height",
                "Field10" => "Depth",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F14_Union)))
        {
            displayName = name switch
            {
                "Field00" => "Enabled",
                "Field01" => "Value",
                _ => string.Empty,
            };
        }
        else if (IsSpecificPlacementType(componentType, nameof(PlacementZone_F14_Sub)))
        {
            displayName = name switch
            {
                "Field00" => "Min",
                "Field01" => "Max",
                _ => string.Empty,
            };
        }

        return displayName.Length != 0;
    }

    public static bool IsSpecificPlacementType(Type componentType, string typeName)
    {
        for (var current = componentType; current != null; current = current.BaseType)
        {
            if (current.Name == typeName)
                return true;
        }

        return false;
    }

    private static bool IsHolderType(Type componentType)
    {
        for (var current = componentType; current != null; current = current.BaseType)
        {
            if (current.Name.EndsWith("Holder", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsCleanableString(PropertyDescriptor descriptor)
    {
        return descriptor.Name is
            "NameModel" or
            "NameAnimation" or
            "ParticleFile" or
            "PlayName" or
            "StopName" or
            "Field02";
    }

    private static string SplitGeneratedName(string name)
    {
        if (name.StartsWith("Field", StringComparison.Ordinal) && name.Length > 5)
            return $"Field {name[5..]}";

        if (name.StartsWith("Hash", StringComparison.Ordinal) && name.Length > 4)
            return $"Hash {name[4..]}";

        if (name.StartsWith("Byte", StringComparison.Ordinal) && name.Length > 4)
            return $"Byte {name[4..]}";

        return name;
    }
}

internal sealed class PlacementObjectConverter(TypeConverter fallback) : ExpandableObjectConverter
{
    public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;

    public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object value, Attribute[]? attributes)
    {
        TypeRegistrationHelper.RegisterIListConvertersRecursively(value.GetType());
        return TypeDescriptor.GetProperties(value, attributes, false);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || fallback.CanConvertTo(context, destinationType) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string))
            return value?.ToString() ?? string.Empty;

        return fallback.CanConvertTo(context, destinationType)
            ? fallback.ConvertTo(context, culture, value, destinationType)
            : base.ConvertTo(context, culture, value, destinationType);
    }
}

internal sealed class PlacementStringConverter(TypeConverter fallback) : StringConverter
{
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || fallback.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is string text)
        {
            if (text.StartsWith("Play_", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase))
                return PlacementZoneLabelProvider.CleanEvent(text);

            if (text.Contains('/') || text.Contains('\\'))
                return PlacementZoneLabelProvider.CleanPath(text);

            return text;
        }

        return fallback.ConvertTo(context, culture, value, destinationType);
    }
}

internal sealed class PlacementHashConverter(PropertyDescriptor descriptor) : UInt64Converter
{
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is ulong hash)
            return GetDisplayValue(hash);

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text && TryParseHash(text, out var hash))
            return hash;

        return base.ConvertFrom(context, culture, value);
    }

    private string GetDisplayValue(ulong hash)
    {
        if (descriptor.Name == "ZoneID")
            return PlacementZoneLabelProvider.Zone(hash);

        if (descriptor.Name == "HashObjectName")
            return PlacementZoneLabelProvider.Object(hash);

        if (descriptor.Name == "SignHash")
            return PlacementZoneLabelProvider.Sign(hash);

        if (descriptor.Name == "TriggerName")
            return PlacementZoneLabelProvider.Trigger(hash);

        if (descriptor.Name == "PathName")
            return PlacementZoneLabelProvider.Path(hash);

        if (descriptor.Name == "UnlockFlagHash")
            return PlacementZoneLabelProvider.Flag(hash);

        if (descriptor.Name == "TrainerID" &&
            PlacementPropertyGridUtil.IsSpecificPlacementType(descriptor.ComponentType, nameof(PlacementZoneTrainerHolder)))
            return PlacementZoneLabelProvider.Trainer(hash);

        if (descriptor.Name == "MovementPath" &&
            PlacementPropertyGridUtil.IsSpecificPlacementType(descriptor.ComponentType, nameof(PlacementZoneTrainerHolder)))
            return PlacementZoneLabelProvider.Path(hash);

        if (descriptor.Name == "HashModel")
            return PlacementZoneLabelProvider.Model(hash);

        if (descriptor.Name == "SpawnID" &&
            PlacementPropertyGridUtil.IsSpecificPlacementType(descriptor.ComponentType, nameof(PlacementZoneStaticObjectSpawn)))
            return PlacementZoneLabelProvider.StaticSpawn(hash);

        if (PlacementPropertyGridUtil.IsSpecificPlacementType(descriptor.ComponentType, nameof(PlacementZoneHiddenItemChance)) ||
            PlacementPropertyGridUtil.IsSpecificPlacementType(descriptor.ComponentType, nameof(PlacementZoneBerryTreeRandom)))
            return PlacementZoneLabelProvider.Item(hash);

        if (descriptor.Name.StartsWith("Hash", StringComparison.Ordinal))
            return PlacementZoneLabelProvider.Hash(hash);

        return PlacementZoneLabelProvider.Hash(hash);
    }

    private static bool TryParseHash(string text, out ulong hash)
    {
        var token = text.Trim();
        if (token.Length == 0)
        {
            hash = 0;
            return false;
        }

        var hexToken = token.Split([' ', '(', ')', '[', ']', ':', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(z => z.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || z.All(Uri.IsHexDigit) && z.Length is >= 8 and <= 16);
        if (hexToken != null)
        {
            if (hexToken.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hexToken = hexToken[2..];
            return ulong.TryParse(hexToken, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hash);
        }

        return ulong.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out hash);
    }
}

internal sealed class PlacementItemHashConverter : UInt64Converter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        => new(PlacementZoneLabelProvider.ItemHashes.Cast<object>().ToArray());

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is ulong hash)
            return PlacementZoneLabelProvider.Item(hash);

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text && PlacementZoneLabelProvider.TryGetItemHash(text, out var hash))
            return hash;

        return base.ConvertFrom(context, culture, value);
    }
}
