using System;
using System.Linq;
using System.Numerics;
using System.Text;
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


    public class HexUTF8StringConvertor : IHexConvertor<String>
    {
        public string ConvertToHex(String value)
        {
            return Encoding.UTF8.GetBytes(value).ToHexString();
        }

        public String ConvertFromHex(string newHexValue)
        {
           var bytes = newHexValue.HexStringToByteArray();
           return System.Text.Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

    }
}