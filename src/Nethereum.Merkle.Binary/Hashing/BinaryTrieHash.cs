using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.Hashing
{
    // EIP-7864: hash([0x00] * 64) = [0x00] * 32
    // This zero-propagation shortcut applies to all 64-byte inputs in the
    // tree merkleization (internal nodes, stem nodes, value pairs). It does
    // NOT apply to 32-byte leaf values or to key derivation.
    internal static class BinaryTrieHash
    {
        internal static byte[] Compute(IHashProvider provider, byte[] data)
        {
            if (data.Length == 64 && IsAllZero(data))
                return new byte[BinaryTrieConstants.HashSize];

            return provider.ComputeHash(data);
        }

        private static bool IsAllZero(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
                if (data[i] != 0) return false;
            return true;
        }
    }
}
