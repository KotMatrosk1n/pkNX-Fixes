// This is free and unencumbered software released into the public domain.
// 
// Anyone is free to copy, modify, publish, use, compile, sell, or
// distribute this software, either in source code form or as a compiled
// binary, for any purpose, commercial or non-commercial, and by any
// means.
// 
// In jurisdictions that recognize copyright laws, the author or authors
// of this software dedicate any and all copyright interest in the
// software to the public domain.We make this dedication for the benefit
// of the public at large and to the detriment of our heirs and
// successors. We intend this dedication to be an overt act of
// relinquishment in perpetuity of all present and future rights to this
// software under copyright law.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
// OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
// 
// For more information, please refer to <https://unlicense.org/>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using System.Reflection;
using System.Threading;
using pkNX.Structures;

namespace pkNX.WinForms;

public static class TypeRegistrationHelper
{
    // Keep track of types we’ve already processed.
    private static readonly HashSet<Type> RegisteredTypes = [];
    private static readonly Lock LockObj = new();

    /// <summary>
    /// Scans the type (and its nested types) for any properties of type IList&lt;&gt;
    /// and registers a custom type descriptor provider for types that have such properties.
    /// </summary>
    public static void RegisterIListConvertersRecursively(Type type)
    {
        lock (LockObj)
        {
            if (!RegisteredTypes.Add(type))
                return;
        }

        var registeredProvider = IsShopType(type);
        if (registeredProvider)
            AddProvider(type);

        // Check each public instance property.
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propType = prop.PropertyType;

            // Is it a generic type and exactly an IList<>
            if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(IList<>))
            {
                if (!registeredProvider)
                {
                    // For this type, add our custom provider that overrides the IList property converter.
                    // (The provider itself will decide which property to wrap based on its name or other criteria.)
                    AddProvider(type);
                    registeredProvider = true;
                }

                // Also, register for the element type in case it in turn contains IList<> properties.
                var elementType = propType.GetGenericArguments()[0];
                if (elementType.IsClass && elementType != typeof(string))
                    RegisterIListConvertersRecursively(elementType);
            }
            else if (propType.IsClass && propType != typeof(string))
            {
                // Also check non-list properties recursively.
                RegisterIListConvertersRecursively(propType);
            }
        }
    }

    private static void AddProvider(Type type)
    {
        TypeDescriptor.AddProvider(new DynamicListTypeDescriptionProvider(type), type);
        TypeDescriptor.Refresh(type);
    }

    public static bool IsShopType(Type type)
    {
        var fullName = type.FullName;
        return fullName is
            "pkNX.Structures.FlatBuffers.LGPE.SingleShop" or
            "pkNX.Structures.FlatBuffers.LGPE.MultiShop" or
            "pkNX.Structures.FlatBuffers.SWSH.SingleShop" or
            "pkNX.Structures.FlatBuffers.SWSH.MultiShop";
    }

    public static bool IsShopInventory(Type type)
    {
        var fullName = type.FullName;
        return fullName is
            "pkNX.Structures.FlatBuffers.LGPE.Inventory" or
            "pkNX.Structures.FlatBuffers.SWSH.Inventory" or
            "pkNX.WinForms.ShopInventoryPropertyGridObject";
    }
}
public class DynamicListTypeDescriptionProvider(Type type)
    : TypeDescriptionProvider(TypeDescriptor.GetProvider(type))
{
    private readonly TypeDescriptionProvider _baseProvider = TypeDescriptor.GetProvider(type);
    private readonly Type _registeredType = type;

    public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object? instance)
    {
        var baseDescriptor = _baseProvider.GetTypeDescriptor(objectType, instance);
        return new DynamicListTypeDescriptor(baseDescriptor, objectType ?? _registeredType);
    }
}

public class DynamicListTypeDescriptor(ICustomTypeDescriptor? parent, Type objectType)
    : CustomTypeDescriptor(parent)
{
    public override PropertyDescriptorCollection GetProperties() => GetFilteredProperties(base.GetProperties());

    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
        => GetFilteredProperties(base.GetProperties(attributes));

    private PropertyDescriptorCollection GetFilteredProperties(PropertyDescriptorCollection originalProps)
    {
        List<PropertyDescriptor> newProps = [];

        foreach (PropertyDescriptor pd in originalProps)
        {
            if (pd.Name == "Hash" && TypeRegistrationHelper.IsShopType(objectType))
                continue;

            // Check if this is our target property.
            // For example, check by name or by type.
            var type = pd.PropertyType;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
            {
                // Wrap the property descriptor with one that supplies a custom type converter.
                newProps.Add(new DynamicListPropertyDescriptor(pd));
            }
            else
            {
                newProps.Add(pd);
            }
        }

        return new PropertyDescriptorCollection(newProps.ToArray(), true);
    }
}

public class DynamicListPropertyDescriptor(PropertyDescriptor baseDescriptor)
    : PropertyDescriptor(baseDescriptor)
{
    public override bool CanResetValue(object component) => baseDescriptor.CanResetValue(component);
    public override Type ComponentType => baseDescriptor.ComponentType;
    public override object? GetValue(object? component) => baseDescriptor.GetValue(component);
    public override bool IsReadOnly => baseDescriptor.IsReadOnly;
    public override Type PropertyType => baseDescriptor.PropertyType;
    public override void ResetValue(object component) => baseDescriptor.ResetValue(component);
    public override void SetValue(object? component, object? value) => baseDescriptor.SetValue(component, value);
    public override bool ShouldSerializeValue(object component) => baseDescriptor.ShouldSerializeValue(component);

    public override object? GetEditor(Type editorBaseType)
    {
        if (editorBaseType == typeof(UITypeEditor) && IsShopItemList())
            return new ShopItemListUITypeEditor();

        return baseDescriptor.GetEditor(editorBaseType);
    }

    private bool IsShopItemList()
    {
        if (baseDescriptor.Name != "Items" || !TypeRegistrationHelper.IsShopInventory(baseDescriptor.ComponentType))
            return false;

        var type = baseDescriptor.PropertyType;
        return type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(IList<>) &&
            type.GetGenericArguments()[0] == typeof(int);
    }

    public override TypeConverter Converter
    {
        get
        {
            // Get the element type (T) from IList<T>
            var elementType = PropertyType.GetGenericArguments().FirstOrDefault();
            if (elementType == null)
                return baseDescriptor.Converter;

            // Create an instance of our custom converter, e.g. ListTypeConverter<T>.
            var converterType = typeof(ListTypeConverter<>).MakeGenericType(elementType);
            if (Activator.CreateInstance(converterType, baseDescriptor) is TypeConverter converter)
                return converter;
            return baseDescriptor.Converter;
        }
    }
}

public class ListTypeConverter<T>(PropertyDescriptor listDescriptor) : ExpandableObjectConverter
{
    private readonly bool IsShopItemList = IsShopItemListProperty(listDescriptor);
    private readonly TypeConverter ElementConverter = GetElementConverter(listDescriptor);

    private static TypeConverter GetElementConverter(PropertyDescriptor listDescriptor)
    {
        if (IsShopItemListProperty(listDescriptor))
            return new ItemConverter();

        return TypeDescriptor.GetConverter(typeof(T));
    }

    private static bool IsShopItemListProperty(PropertyDescriptor listDescriptor)
    {
        return typeof(T) == typeof(int) &&
            listDescriptor.Name == "Items" &&
            TypeRegistrationHelper.IsShopInventory(listDescriptor.ComponentType);
    }

    public override bool GetPropertiesSupported(ITypeDescriptorContext? context)
    {
        return !IsShopItemList;
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is IEnumerable<T> list)
            return string.Join(", ", list.Select(item => ConvertItemToString(context, culture, item)));

        return base.ConvertTo(context, culture, value, destinationType);
    }

    private string ConvertItemToString(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, T? item)
    {
        if (item is null)
            return string.Empty;

        if (IsShopItemList && item is int itemID)
            return ShopItemNameFormatter.GetDisplayName(itemID);

        if (ElementConverter.CanConvertTo(context, typeof(string)))
            return ElementConverter.ConvertToString(context, culture, item) ?? string.Empty;

        return item.ToString() ?? string.Empty;
    }

    public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object value, Attribute[]? attributes)
    {
        if (value is not IList<T?> list)
            return base.GetProperties(context, value, attributes);

        var props = new PropertyDescriptor[list.Count];
        for (int i = 0; i < list.Count; i++)
            props[i] = new ListItemPropertyDescriptor<T>(list, i, ElementConverter);

        return new PropertyDescriptorCollection(props);
    }
}

public class ListItemPropertyDescriptor<T>(IList<T?> list, int index, TypeConverter converter)
    : PropertyDescriptor($"[{index}]", null)
{
    public override object? GetValue(object? component) => list[index];
    public override void SetValue(object? component, object? value)
    {
        if (value is string s && converter.CanConvertFrom(typeof(string)))
            value = converter.ConvertFromInvariantString(s);

        list[index] = (T?)value;
    }
    public override bool IsReadOnly => false;
    public override Type ComponentType => list.GetType();
    public override bool CanResetValue(object component) => false;
    public override TypeConverter Converter => converter;
    public override Type PropertyType => typeof(T);
    public override void ResetValue(object component) { }
    public override bool ShouldSerializeValue(object component) => true;
}
