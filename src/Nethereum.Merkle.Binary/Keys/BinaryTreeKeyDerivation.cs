using System;
using System.Numerics;
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

        private static readonly BigInteger MainStorageOffset = BigInteger.Pow(256, BinaryTrieConstants.StemSize);

        private readonly IHashProvider _hashProvider;

        public BinaryTreeKeyDerivation(IHashProvider hashProvider)
        {
            _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
        }

        public byte[] GetTreeKey(byte[] address32, BigInteger treeIndex, byte subIndex)
        {
            if (address32 == null || address32.Length != 32)
                throw new ArgumentException("address32 must be 32 bytes");
            if (treeIndex.Sign < 0)
                throw new ArgumentException("treeIndex must be non-negative", nameof(treeIndex));

            var tiBytes = new byte[32];
            if (treeIndex.Sign > 0)
            {
                var raw = treeIndex.ToByteArrayUnsignedBigEndian();
                for (int i = 0; i < raw.Length && i < 32; i++)
                    tiBytes[i] = raw[raw.Length - 1 - i];
            }

            var input = new byte[64];
            Array.Copy(address32, 0, input, 0, 32);
            Array.Copy(tiBytes, 0, input, 32, 32);

            var digest = _hashProvider.ComputeHash(input);

            var key = new byte[32];
            Array.Copy(digest, 0, key, 0, BinaryTrieConstants.StemSize);
            key[31] = subIndex;
            return key;
        }

        public byte[] GetTreeKeyForBasicData(byte[] address)
        {
            return GetTreeKey(AddressTo32(address), BigInteger.Zero, BasicDataLeafKey);
        }

        public byte[] GetTreeKeyForCodeHash(byte[] address)
        {
            return GetTreeKey(AddressTo32(address), BigInteger.Zero, CodeHashLeafKey);
        }

        public byte[] GetTreeKeyForCodeChunk(byte[] address, ulong chunkId)
        {
            ulong pos = (ulong)CodeOffset + chunkId;
            var treeIndex = new BigInteger(pos / BinaryTrieConstants.StemNodeWidth);
            byte subIndex = (byte)(pos % BinaryTrieConstants.StemNodeWidth);
            return GetTreeKey(AddressTo32(address), treeIndex, subIndex);
        }

        public byte[] GetTreeKeyForStorageSlot(byte[] address, BigInteger storageKey)
        {
            if (storageKey.Sign < 0)
                throw new ArgumentException("storageKey must be non-negative", nameof(storageKey));

            const int inlineThreshold = CodeOffset - HeaderStorageOffset;

            BigInteger pos;
            if (storageKey >= 0 && storageKey < inlineThreshold)
            {
                pos = HeaderStorageOffset + storageKey;
            }
            else
            {
                pos = MainStorageOffset + storageKey;
            }

            var stemWidth = new BigInteger(BinaryTrieConstants.StemNodeWidth);
            var treeIndex = BigInteger.Divide(pos, stemWidth);
            var subMod = BigInteger.Remainder(pos, stemWidth);
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
