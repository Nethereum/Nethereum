using Nethereum.Hex.HexConvertors;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Hex.HexTypes
{
    public class HexRPCType<T>
    {
        protected IHexConvertor<T> convertor;

        protected string hexValue;

        protected T value;

        protected HexRPCType(IHexConvertor<T> convertor)
        {
            this.convertor = convertor;
        }

        public HexRPCType(IHexConvertor<T> convertor, string hexValue)
        {
            this.convertor = convertor;
            InitialiseFromHex(hexValue);
        }

        public HexRPCType(T value, IHexConvertor<T> convertor)
        {
            this.convertor = convertor;
            InitialiseFromValue(value);
        }

        public string HexValue
        {
            get => hexValue;
            set => InitialiseFromHex(value);
        }

        public T Value
        {
            get => value;
            set => InitialiseFromValue(value);
        }

        protected void InitialiseFromHex(string newHexValue)
        {
            value = ConvertFromHex(newHexValue);
            hexValue = newHexValue.EnsureHexPrefix();
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

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}