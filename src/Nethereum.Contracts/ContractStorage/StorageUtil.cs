using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Nethereum.Contracts.ContractStorage
{
    public class StorageUtil
    {
        public static byte[] CalculateMappingAddressStorageKey(string address, ulong slot)
        {
            return new ABIEncode().GetSha3ABIEncoded(new ABIValue("address", address), new ABIValue("int", slot));
        }

        public static BigInteger CalculateMappingAddressStorageKeyAsBigInteger(string address, ulong slot)
        {
            return CalculateMappingAddressStorageKey(address, slot).ToHex().HexToBigInteger(false);
        }
    }
}
