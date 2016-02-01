using System;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Hex.HexConvertors
{
    public class HexUTF8StringConvertor : IHexConvertor<string>
    {
        public string ConvertToHex(String value)
        {
            return value.ToHexUTF8();
        }

        public String ConvertFromHex(string hex)
        {
            return hex.HexToUTF8String();
        }

    }
}