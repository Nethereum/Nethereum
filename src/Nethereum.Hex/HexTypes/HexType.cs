using System;
using Nethereum.Hex.HexConvertors;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Hex.HexTypes
{
    public class HexRPCType<T>: IEquatable<HexRPCType<T>>
    {
        protected IHexConvertor<T> convertor;

        protected string hexValue;

        protected T value;

        protected object lockingObject = new object();
        protected bool needsInitialisingValue;

        protected T GetValue()
        {
            lock (lockingObject)
            {
                if (needsInitialisingValue)
                {
                    InitialiseValueFromHex(hexValue);
                    needsInitialisingValue = false;
                }
                return value;
            }
        }
        protected HexRPCType(IHexConvertor<T> convertor)
        {
            this.convertor = convertor;
        }

        public HexRPCType(IHexConvertor<T> convertor, string hexValue)
        {
            this.convertor = convertor;
            SetHexAndFlagValueToBeInitialised(hexValue);
        }

        public HexRPCType(T value, IHexConvertor<T> convertor)
        {
            this.convertor = convertor;
            InitialiseFromValue(value);
        }

        public string HexValue
        {
            get => hexValue;
            set => SetHexAndFlagValueToBeInitialised(value);
        }

        public T Value
        {
            get => GetValue();
            set => InitialiseFromValue(value);
        }

        protected void SetHexAndFlagValueToBeInitialised(string newHexValue)
        {
            hexValue = newHexValue.EnsureHexPrefix();
            lock (lockingObject)
            {
                needsInitialisingValue = true;
            }
        }

        protected void InitialiseValueFromHex(string newHexValue)
        {
            value = ConvertFromHex(newHexValue);
        }

        protected void InitialiseFromValue(T newValue)
        {
            hexValue = ConvertToHex(newValue).EnsureHexPrefix();
            value = newValue;
        }

        protected string ConvertToHex(T newValue)
        {
            return convertor.ConvertToHex(newValue);
        }

        protected T ConvertFromHex(string newHexValue)
        {
            return convertor.ConvertFromHex(newHexValue);
        }

        public byte[] ToHexByteArray()
        {
            return HexValue.HexToByteArray();
        }

        public static implicit operator byte[](HexRPCType<T> hexRpcType)
        {
            return hexRpcType.ToHexByteArray();
        }

        public static implicit operator T(HexRPCType<T> hexRpcType)
        {
            return hexRpcType.Value;
        }

        public static bool operator == (HexRPCType<T> lhs, HexRPCType<T> rhs)
        {
            // Check for null on left side.
            if (lhs is null)
            {
                if (rhs is null)
                {
                    // null == null = true.
                    return true;
                }

                // Only the left side is null.
                return false;
            }
            // Equals handles case of null on right side.
            return lhs.Equals(rhs);
        }

        public static bool operator != (HexRPCType<T> lhs, HexRPCType<T> rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public bool Equals(HexRPCType<T> other)
        {
            return other == null  ? false : Value.Equals(other.Value);
        }
    }
}