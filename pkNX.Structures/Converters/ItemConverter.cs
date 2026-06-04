using System;
using System.ComponentModel;
using System.Globalization;

namespace pkNX.Structures;

public class ItemConverter : TypeConverter
{
    public static string[] ItemNames { get; set; } = [];

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s)
        {
            if (int.TryParse(s, NumberStyles.Integer, culture, out var itemID))
                return itemID;

            var index = Array.IndexOf(ItemNames, s);
            if (index >= 0)
                return index;
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is int i)
            return (uint)i < (uint)ItemNames.Length ? ItemNames[i] : i.ToString(culture);

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        return new StandardValuesCollection(ItemNames);
    }
}
