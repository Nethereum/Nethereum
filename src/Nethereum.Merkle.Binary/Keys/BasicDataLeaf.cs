using System;
using Nethereum.Util;

namespace Nethereum.Merkle.Binary.Keys
{
    public static class BasicDataLeaf
    {
        public const int VersionOffset = 0;
        public const int CodeSizeOffset = 5;
        public const int NonceOffset = 8;
        public const int BalanceOffset = 16;

        public static byte[] Pack(byte version, uint codeSize, ulong nonce, EvmUInt256 balance)
        {
            if (balance.U2 != 0 || balance.U3 != 0)
                throw new ArgumentOutOfRangeException(nameof(balance), "balance exceeds the 128-bit BasicData leaf field");

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

            ulong balHi = balance.U1;
            ulong balLo = balance.U0;

            leaf[BalanceOffset] = (byte)(balHi >> 56);
            leaf[BalanceOffset + 1] = (byte)(balHi >> 48);
            leaf[BalanceOffset + 2] = (byte)(balHi >> 40);
            leaf[BalanceOffset + 3] = (byte)(balHi >> 32);
            leaf[BalanceOffset + 4] = (byte)(balHi >> 24);
            leaf[BalanceOffset + 5] = (byte)(balHi >> 16);
            leaf[BalanceOffset + 6] = (byte)(balHi >> 8);
            leaf[BalanceOffset + 7] = (byte)balHi;

            leaf[BalanceOffset + 8] = (byte)(balLo >> 56);
            leaf[BalanceOffset + 9] = (byte)(balLo >> 48);
            leaf[BalanceOffset + 10] = (byte)(balLo >> 40);
            leaf[BalanceOffset + 11] = (byte)(balLo >> 32);
            leaf[BalanceOffset + 12] = (byte)(balLo >> 24);
            leaf[BalanceOffset + 13] = (byte)(balLo >> 16);
            leaf[BalanceOffset + 14] = (byte)(balLo >> 8);
            leaf[BalanceOffset + 15] = (byte)balLo;

            return leaf;
        }

        public static void Unpack(byte[] leaf, out byte version, out uint codeSize, out ulong nonce, out EvmUInt256 balance)
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

            ulong balHi = (ulong)leaf[BalanceOffset] << 56
                        | (ulong)leaf[BalanceOffset + 1] << 48
                        | (ulong)leaf[BalanceOffset + 2] << 40
                        | (ulong)leaf[BalanceOffset + 3] << 32
                        | (ulong)leaf[BalanceOffset + 4] << 24
                        | (ulong)leaf[BalanceOffset + 5] << 16
                        | (ulong)leaf[BalanceOffset + 6] << 8
                        | leaf[BalanceOffset + 7];

            ulong balLo = (ulong)leaf[BalanceOffset + 8] << 56
                        | (ulong)leaf[BalanceOffset + 9] << 48
                        | (ulong)leaf[BalanceOffset + 10] << 40
                        | (ulong)leaf[BalanceOffset + 11] << 32
                        | (ulong)leaf[BalanceOffset + 12] << 24
                        | (ulong)leaf[BalanceOffset + 13] << 16
                        | (ulong)leaf[BalanceOffset + 14] << 8
                        | leaf[BalanceOffset + 15];

            balance = new EvmUInt256(0UL, 0UL, balHi, balLo);
        }
    }
}
