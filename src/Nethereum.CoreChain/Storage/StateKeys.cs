using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain.Storage
{
    public static class StateKeys
    {
        public static byte[] AccountKey(string address)
        {
            var raw = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant().HexToByteArray();
            return AccountKey((ReadOnlySpan<byte>)raw);
        }

        public static byte[] AccountKey(EvmAddress address) => AccountKey(address.AsSpan());

        public static byte[] AccountKey(ReadOnlySpan<byte> addressBytes) => Sha3Keccack.Current.CalculateHash(addressBytes.ToArray());

        public static string AccountKeyHex(string address) => AccountKey(address).ToHex();

        public static string AccountKeyHex(EvmAddress address) => AccountKey(address).ToHex();

        public static byte[] StorageSlotKey(BigInteger slot)
        {
            var slotBytes = slot.ToBytesForRLPEncoding().PadBytes(32);
            return Sha3Keccack.Current.CalculateHash(slotBytes);
        }

        public static string StorageSlotKeyHex(BigInteger slot) => StorageSlotKey(slot).ToHex();
    }
}
