using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.Hashing
{
    public static class ValuesMerkleizer
    {
        public static byte[] Merkleize(byte[][] values, IHashProvider hashProvider)
        {
            var data = new byte[BinaryTrieConstants.StemNodeWidth][];

            for (int i = 0; i < BinaryTrieConstants.StemNodeWidth; i++)
            {
                if (values != null && i < values.Length && values[i] != null)
                {
                    data[i] = hashProvider.ComputeHash(values[i]);
                }
            }

            for (int level = 1; level <= BinaryTrieConstants.ValueMerkleLevels; level++)
            {
                int count = BinaryTrieConstants.StemNodeWidth / (1 << level);
                for (int i = 0; i < count; i++)
                {
                    var left = data[i * 2];
                    var right = data[i * 2 + 1];

                    if (left == null && right == null)
                    {
                        data[i] = null;
                        continue;
                    }

                    var pair = new byte[BinaryTrieConstants.HashSize * 2];
                    if (left != null)
                        System.Array.Copy(left, 0, pair, 0, BinaryTrieConstants.HashSize);
                    if (right != null)
                        System.Array.Copy(right, 0, pair, BinaryTrieConstants.HashSize, BinaryTrieConstants.HashSize);

                    data[i] = BinaryTrieHash.Compute(hashProvider, pair);
                }
            }

            return data[0] ?? new byte[BinaryTrieConstants.HashSize];
        }
    }
}
