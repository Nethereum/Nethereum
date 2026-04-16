using System;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.Keys
{
    public class BinaryTreeKeyDerivation
    {
        public const byte BasicDataLeafKey = 0;
        public const byte CodeHashLeafKey = 1;
        public const int HeaderStorageOffset = 64;
        public const int CodeOffset = 128;

        // 256^31 = 2^248; u3 = 1 << 56 places the single bit at position 248.
        private static readonly EvmUInt256 MainStorageOffset = new EvmUInt256(1UL << 56, 0UL, 0UL, 0UL);

        private readonly IHashProvider _hashProvider;

        public BinaryTreeKeyDerivation(IHashProvider hashProvider)
        {
            _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
        }

        public byte[] GetTreeKey(byte[] address32, EvmUInt256 treeIndex, byte subIndex)
        {
            if (address32 == null || address32.Length != 32)
                throw new ArgumentException("address32 must be 32 bytes");

            var input = new byte[64];
            Array.Copy(address32, 0, input, 0, 32);
            var tiBytes = treeIndex.ToLittleEndian();
            Array.Copy(tiBytes, 0, input, 32, 32);

            var digest = _hashProvider.ComputeHash(input);

            var key = new byte[32];
            Array.Copy(digest, 0, key, 0, BinaryTrieConstants.StemSize);
            key[31] = subIndex;
            return key;
        }

        public byte[] GetTreeKeyForBasicData(byte[] address)
        {
            return GetTreeKey(AddressTo32(address), EvmUInt256.Zero, BasicDataLeafKey);
        }

        public byte[] GetTreeKeyForCodeHash(byte[] address)
        {
            return GetTreeKey(AddressTo32(address), EvmUInt256.Zero, CodeHashLeafKey);
        }

        public byte[] GetTreeKeyForCodeChunk(byte[] address, ulong chunkId)
        {
            ulong pos = (ulong)CodeOffset + chunkId;
            var treeIndex = (EvmUInt256)(pos / BinaryTrieConstants.StemNodeWidth);
            byte subIndex = (byte)(pos % BinaryTrieConstants.StemNodeWidth);
            return GetTreeKey(AddressTo32(address), treeIndex, subIndex);
        }

        public byte[] GetTreeKeyForStorageSlot(byte[] address, EvmUInt256 storageKey)
        {
            const int inlineThreshold = CodeOffset - HeaderStorageOffset;

            EvmUInt256 pos;
            if (storageKey < (EvmUInt256)inlineThreshold)
            {
                pos = (EvmUInt256)HeaderStorageOffset + storageKey;
            }
            else
            {
                pos = MainStorageOffset + storageKey; // wraps mod 2^256, matching EIP-6800 spec
            }

            var stemWidth = (EvmUInt256)BinaryTrieConstants.StemNodeWidth;
            var treeIndex = pos / stemWidth;
            var subMod = pos % stemWidth;
            return GetTreeKey(AddressTo32(address), treeIndex, (byte)subMod);
        }

        public static byte[] AddressTo32(byte[] address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            var addr32 = new byte[32];
            int offset = 32 - address.Length;
            if (offset < 0) offset = 0;
            int len = Math.Min(address.Length, 32);
            Array.Copy(address, 0, addr32, offset, len);
            return addr32;
        }
    }
}
