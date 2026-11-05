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
        public static byte[] AddressBytes(string address)
        {
            return AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant().HexToByteArray();
        }

        public static byte[] AccountKey(string address)
        {
            return AccountKey((ReadOnlySpan<byte>)AddressBytes(address));
        }

        public static byte[] AccountKey(EvmAddress address) => AccountKey(address.AsSpan());

        public static byte[] AccountKey(EvmAddress address, IStateStore store)
        {
            if (store is not IAddressHashCache cache) return AccountKey(address);
            if (cache.TryGetAddressHash(address, out var cached)) return cached;
            var computed = AccountKey(address);
            cache.SetAddressHash(address, computed);
            return computed;
        }

        public static byte[] AccountKey(ReadOnlySpan<byte> addressBytes) => Sha3Keccack.Current.CalculateHash(addressBytes.ToArray());

        public static string AccountKeyHex(string address) => AccountKey(address).ToHex();

        public static string AccountKeyHex(EvmAddress address) => AccountKey(address).ToHex();

        public static byte[] StorageSlotBytes(BigInteger slot)
        {
            return slot.ToBytesForRLPEncoding().PadBytes(32);
        }

        public static byte[] StorageSlotKey(BigInteger slot)
        {
            return Sha3Keccack.Current.CalculateHash(StorageSlotBytes(slot));
        }

        public static string StorageSlotKeyHex(BigInteger slot) => StorageSlotKey(slot).ToHex();
    }
}
