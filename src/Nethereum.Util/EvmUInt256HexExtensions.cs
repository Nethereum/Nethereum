using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;

namespace Nethereum.Util
{
    public static class EvmUInt256HexExtensions
    {
        public static EvmUInt256 ToEvmUInt256(this HexBigInteger hex)
        {
            if (hex == null) return EvmUInt256.Zero;
            var hexStr = hex.HexValue;
            if (string.IsNullOrEmpty(hexStr) || hexStr == "0x" || hexStr == "0x0")
                return EvmUInt256.Zero;
            return EvmUInt256.FromHex(hexStr);
        }

        public static HexBigInteger ToHexBigInteger(this EvmUInt256 value)
        {
            return new HexBigInteger(value.ToHexString());
        }

        public static EvmInt256 ToEvmInt256(this HexBigInteger hex)
        {
            return (EvmInt256)hex.ToEvmUInt256();
        }

        public static HexBigInteger ToHexBigInteger(this EvmInt256 value)
        {
            return new HexBigInteger(((EvmUInt256)value).ToHexString());
        }
    }
}
