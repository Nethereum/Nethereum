using System;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ABI.Encoders
{
    public class AddressTypeEncoder : ITypeEncoder
    {
        private IntTypeEncoder intTypeEncoder;

        public AddressTypeEncoder()
        {
            this.intTypeEncoder = new IntTypeEncoder();
        }

        public byte[] Encode(object value)
        {
            var strValue = value as string;

            if (strValue != null
                && !strValue.StartsWith("0x", StringComparison.Ordinal))
            {
                // address is supposed to be always in hex
                value = "0x" + value;
            }

            byte[] addr = intTypeEncoder.Encode(value);

            for (int i = 0; i < 12; i++)
            {
                if (addr[i] != 0 && addr[i] != 0xFF)
                {
                    throw new Exception("Invalid address (should be 20 bytes length): " + addr.ToHex());
                }
            }
            return addr;
        }
    }
}