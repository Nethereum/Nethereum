using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// Canonical Ethereum state-trie key derivation. Yellow Paper §4.1: the world-state
    /// trie maps <c>keccak256(address) → account RLP</c>, and every contract's storage
    /// trie maps <c>keccak256(slot) → value RLP</c>. Snap/1 (EIP-2364) and EIP-1186
    /// proofs both expose the trie in this shape — when a database stores its accounts
    /// and slots under the same keys the trie uses, no translation layer is needed.
    ///
    /// Single source of truth for how an <see cref="IStateStore"/> derives a database
    /// key from a caller-supplied address / slot. Implementations delegate here so the
    /// cache key equals the trie key by construction.
    /// </summary>
    public static class StateKeys
    {
        private static readonly Sha3Keccack Keccak = new();

        /// <summary>32-byte <c>keccak256(addressBytes)</c> for the canonical account key.</summary>
        public static byte[] AccountKey(string address)
        {
            var raw = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant().HexToByteArray();
            return Keccak.CalculateHash(raw);
        }

        /// <summary>Lower-case hex of <see cref="AccountKey"/>, prefix-free (no <c>0x</c>).</summary>
        public static string AccountKeyHex(string address) => AccountKey(address).ToHex();

        /// <summary>32-byte <c>keccak256(slotBytes)</c> for the canonical storage-slot key.</summary>
        public static byte[] StorageSlotKey(BigInteger slot)
        {
            var slotBytes = slot.ToBytesForRLPEncoding().PadBytes(32);
            return Keccak.CalculateHash(slotBytes);
        }

        /// <summary>Lower-case hex of <see cref="StorageSlotKey"/>, prefix-free.</summary>
        public static string StorageSlotKeyHex(BigInteger slot) => StorageSlotKey(slot).ToHex();
    }
}
