using System;

namespace Ethereum.ABI.Tests.DNX
{
    public class AddressType : IntType
    {
        public AddressType() : base("address")
        {
        }

        public override byte[] Encode(object value)
        {
            var strValue = value as string;
            if (strValue != null
                && !strValue.StartsWith("0x", StringComparison.Ordinal))
            {
                // address is supposed to be always in hex
                value = "0x" + value;
            }
            byte[] addr = base.Encode(value);
            for (int i = 0; i < 12; i++)
            {
                if (addr[i] != 0)
                {
                    throw new Exception("Invalid address (should be 20 bytes length): " + addr.ToHexString());
                }
            }
            return addr;
        }
    }
}