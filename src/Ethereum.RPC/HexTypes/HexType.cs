using Ethereum.RPC.Util;

namespace Ethereum.RPC
{
    public class HexRPCType<T>
    {
        protected IHexConvertor<T> convertor;

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

        protected string hexValue;
        public string HexValue
        {
            get { return hexValue; }
            set { InitialiseFromHex(hexValue); }
        }

        protected void InitialiseFromHex(string newHexValue)
        {
            value = ConvertFromHex(newHexValue);
            hexValue = newHexValue;
        }

        protected T value;

        public T Value
        {
            get { return value; }
            set { InitialiseFromValue(value); }
        }

        protected void InitialiseFromValue(T newValue)
        {
            hexValue = ConvertToHex(newValue);
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

        public static implicit operator byte[] (HexRPCType<T> hexRpcType)
        {
            return hexRpcType.ToHexByteArray();
        }

        public static implicit operator T(HexRPCType<T> hexRpcType)
        {
            return hexRpcType.Value;
        }


    }
}