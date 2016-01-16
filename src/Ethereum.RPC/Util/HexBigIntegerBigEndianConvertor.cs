using System;
using System.Linq;
using System.Numerics;
using Ethereum.RPC.Util;

namespace Ethereum.RPC
{
    public class HexBigIntegerBigEndianConvertor: IHexConvertor<BigInteger>
    {
        public string ConvertToHex(BigInteger newValue)
        {
            byte[] bytes;

            if (BitConverter.IsLittleEndian)
            {
                bytes = newValue.ToByteArray().Reverse().ToArray();
            }
            else
            {
                bytes = newValue.ToByteArray().ToArray();
            }

            return bytes.ToHexString();
        }

        public BigInteger ConvertFromHex(string newHexValue)
        {
            var encoded = newHexValue.HexStringToByteArray();

            if (BitConverter.IsLittleEndian)
            {
                encoded = encoded.ToArray().Reverse().ToArray();
            }
            return new BigInteger(encoded);
        }

    }
}