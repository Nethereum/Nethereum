using System;
using System.Numerics;
using Nethereum.Util;

namespace Nethereum.Merkle.Binary.Keys
{
    public static class BasicDataLeaf
    {
        public const int VersionOffset = 0;
        public const int CodeSizeOffset = 5;
        public const int NonceOffset = 8;
        public const int BalanceOffset = 16;

        public static byte[] Pack(byte version, uint codeSize, ulong nonce, BigInteger balance)
        {
            var leaf = new byte[BinaryTrieConstants.HashSize];

            leaf[VersionOffset] = version;

            leaf[CodeSizeOffset] = (byte)(codeSize >> 16);
            leaf[CodeSizeOffset + 1] = (byte)(codeSize >> 8);
            leaf[CodeSizeOffset + 2] = (byte)codeSize;

            leaf[NonceOffset] = (byte)(nonce >> 56);
            leaf[NonceOffset + 1] = (byte)(nonce >> 48);
            leaf[NonceOffset + 2] = (byte)(nonce >> 40);
            leaf[NonceOffset + 3] = (byte)(nonce >> 32);
            leaf[NonceOffset + 4] = (byte)(nonce >> 24);
            leaf[NonceOffset + 5] = (byte)(nonce >> 16);
            leaf[NonceOffset + 6] = (byte)(nonce >> 8);
            leaf[NonceOffset + 7] = (byte)nonce;

            if (balance.Sign > 0)
            {
                var b = balance.ToByteArrayUnsignedBigEndian();
                int len = Math.Min(b.Length, 16);
                Array.Copy(b, b.Length - len, leaf, BalanceOffset + 16 - len, len);
            }

            return leaf;
        }

        public static void Unpack(byte[] leaf, out byte version, out uint codeSize, out ulong nonce, out BigInteger balance)
        {
            if (leaf == null || leaf.Length < BinaryTrieConstants.HashSize)
                throw new ArgumentException("Leaf must be 32 bytes");

            version = leaf[VersionOffset];

            codeSize = (uint)leaf[CodeSizeOffset] << 16
                     | (uint)leaf[CodeSizeOffset + 1] << 8
                     | leaf[CodeSizeOffset + 2];

            nonce = (ulong)leaf[NonceOffset] << 56
                  | (ulong)leaf[NonceOffset + 1] << 48
                  | (ulong)leaf[NonceOffset + 2] << 40
                  | (ulong)leaf[NonceOffset + 3] << 32
                  | (ulong)leaf[NonceOffset + 4] << 24
                  | (ulong)leaf[NonceOffset + 5] << 16
                  | (ulong)leaf[NonceOffset + 6] << 8
                  | leaf[NonceOffset + 7];

            var balanceBytes = new byte[16];
            Array.Copy(leaf, BalanceOffset, balanceBytes, 0, 16);
            balance = balanceBytes.ToBigIntegerFromUnsignedBigEndian();
        }
    }
}
