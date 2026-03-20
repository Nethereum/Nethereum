using System;

namespace Nethereum.Merkle.Binary
{
    public static class BinaryTrieConstants
    {
        public const int StemNodeWidth = 256;
        public const int StemSize = 31;
        public const int HashSize = 32;
        public const int BitmapSize = 32;
        public const int NodeTypeBytes = 1;
        public const int ValueMerkleLevels = 8;

        public const byte NodeTypeStem = 1;
        public const byte NodeTypeInternal = 2;

        internal static readonly byte[] ZeroHashInternal = new byte[HashSize];

        public static byte[] ZeroHash => new byte[HashSize];

        public static bool IsZeroHash(byte[] hash)
        {
            if (hash == null || hash.Length != HashSize) return false;
            for (int i = 0; i < HashSize; i++)
            {
                if (hash[i] != 0) return false;
            }
            return true;
        }
    }
}
