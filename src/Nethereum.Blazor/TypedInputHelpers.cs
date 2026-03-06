using System;
using System.Collections;
using System.Numerics;
using System.Reflection;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.Blazor
{
    public record BindConverter<T>(Func<T> Getter, Action<T> Setter)
    {
        public T Value
        {
            get => Getter();
            set => Setter(value);
        }
    }

    public static class TypedInputHelpers
    {
        private static readonly AddressUtil AddressUtilInstance = new();

        public static object CreateDefaultInstance(Type itemType)
        {
            if (itemType == typeof(string)) return string.Empty;
            if (itemType == typeof(int)) return 0;
            if (itemType == typeof(uint)) return 0u;
            if (itemType == typeof(long)) return 0L;
            if (itemType == typeof(ulong)) return 0UL;
            if (itemType == typeof(bool)) return false;
            if (itemType == typeof(decimal)) return 0m;
            if (itemType == typeof(BigInteger)) return BigInteger.Zero;
            if (itemType == typeof(byte[])) return new byte[0];
            if (itemType.GetConstructor(Type.EmptyTypes) is not null)
                return Activator.CreateInstance(itemType);
            return null;
        }

        public static bool IsPrimitiveInput(Type type) =>
            type.IsPrimitive ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(bool);

        public static bool SetBigInteger(IList items, int index, string value)
        {
            if (BigInteger.TryParse(value, out var result))
            {
                items[index] = result;
                return true;
            }
            return false;
        }

        public static bool SetByteArray(IList items, int index, string hex)
        {
            if (!string.IsNullOrWhiteSpace(hex) && hex.IsHex())
            {
                items[index] = hex.HexToByteArray();
                return true;
            }
            return false;
        }

        public static bool SetPrimitive(IList items, int index, Type itemType, string value)
        {
            if (value == null) return false;

            try
            {
                object parsed;
                if (itemType == typeof(string)) parsed = value;
                else if (itemType == typeof(int)) parsed = int.Parse(value);
                else if (itemType == typeof(uint)) parsed = uint.Parse(value);
                else if (itemType == typeof(long)) parsed = long.Parse(value);
                else if (itemType == typeof(ulong)) parsed = ulong.Parse(value);
                else if (itemType == typeof(bool)) parsed = bool.Parse(value);
                else if (itemType == typeof(decimal)) parsed = decimal.Parse(value);
                else parsed = value;

                items[index] = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsValidAddress(string value) =>
            !string.IsNullOrWhiteSpace(value) && AddressUtilInstance.IsValidEthereumAddressHexFormat(value);

        public static BindConverter<string> BindString(object model, PropertyInfo prop) => new(() =>
            (string)prop.GetValue(model) ?? "", v => prop.SetValue(model, v));

        public static BindConverter<bool> BindBool(object model, PropertyInfo prop) => new(() =>
            (bool)(prop.GetValue(model) ?? false), v => prop.SetValue(model, v));

        public static BindConverter<int> BindInt(object model, PropertyInfo prop) => new(() =>
            (int)(prop.GetValue(model) ?? 0), v => prop.SetValue(model, v));

        public static BindConverter<uint> BindUInt(object model, PropertyInfo prop) => new(() =>
            (uint)(prop.GetValue(model) ?? 0u), v => prop.SetValue(model, v));

        public static BindConverter<long> BindLong(object model, PropertyInfo prop) => new(() =>
            (long)(prop.GetValue(model) ?? 0L), v => prop.SetValue(model, v));

        public static BindConverter<ulong> BindULong(object model, PropertyInfo prop) => new(() =>
            (ulong)(prop.GetValue(model) ?? 0UL), v => prop.SetValue(model, v));

        public static BindConverter<decimal> BindDecimal(object model, PropertyInfo prop) => new(() =>
            Convert.ToDecimal(prop.GetValue(model) ?? 0), v => prop.SetValue(model, v));

        public static BindConverter<byte> BindByte(object model, PropertyInfo prop) => new(() =>
            (byte)(prop.GetValue(model) ?? (byte)0), v => prop.SetValue(model, v));

        public static BindConverter<sbyte> BindSByte(object model, PropertyInfo prop) => new(() =>
            (sbyte)(prop.GetValue(model) ?? (sbyte)0), v => prop.SetValue(model, v));

        public static BindConverter<short> BindShort(object model, PropertyInfo prop) => new(() =>
            (short)(prop.GetValue(model) ?? (short)0), v => prop.SetValue(model, v));

        public static BindConverter<ushort> BindUShort(object model, PropertyInfo prop) => new(() =>
            (ushort)(prop.GetValue(model) ?? (ushort)0), v => prop.SetValue(model, v));

        public static BindConverter<string> BindBigInteger(object model, PropertyInfo prop) => new(() =>
            prop.GetValue(model)?.ToString() ?? "", v =>
        {
            if (BigInteger.TryParse(v, out var result))
                prop.SetValue(model, result);
        });

        public static BindConverter<string> BindByteArray(object model, PropertyInfo prop) => new(() =>
        {
            var bytes = prop.GetValue(model) as byte[];
            return bytes != null ? bytes.ToHex(true) : "";
        },
        v =>
        {
            if (!string.IsNullOrWhiteSpace(v) && v.IsHex())
                prop.SetValue(model, v.HexToByteArray());
        });
    }
}
