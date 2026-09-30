using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Patricia.ProofVerification
{
    public interface ITrieNodeVerifier
    {
        bool Verify(byte[] expectedHash, byte[] blob, IHashProvider hashProvider);
    }

    public class TrieNodeVerification : ITrieNodeVerifier
    {
        public static TrieNodeVerification Current { get; } = new TrieNodeVerification();

        public bool Verify(byte[] expectedHash, byte[] blob, IHashProvider hashProvider)
        {
            if (blob == null || blob.Length == 0) return true;
            if (expectedHash == null || expectedHash.Length != 32) return true;
            return ByteUtil.AreEqual(hashProvider.ComputeHash(blob), expectedHash);
        }
    }
}
