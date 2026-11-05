using System;
using Nethereum.Documentation;
using Nethereum.Merkle.Patricia.Nodes;
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

        private static readonly IHashProvider KeccakHashProvider = Sha3KeccackHashProvider.Instance;

        [NethereumDocExample(DocSection.ChainInfrastructure, "key-path-storage", "Refuse a blob that does not hash to its reference", Order = 2)]
        public static void RequireAuthentic(Node reference, byte[] blob)
        {
            if (!ProofVerification.Current.TrieNode.Verify(reference.GetHash(), blob, KeccakHashProvider))
                throw new InvalidOperationException(
                    "Path-keyed trie node hash mismatch: stored blob does not match the parent-referenced hash (tampered or corrupt).");
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "key-path-storage", "Check a stored root blob against a state root", Order = 3)]
        public static bool RootBlobHashesTo(byte[] blob, byte[] stateRoot)
        {
            if (stateRoot == null || stateRoot.Length != 32) return false;
            if (blob == null || blob.Length == 0) return false;
            return ByteUtil.AreEqual(KeccakHashProvider.ComputeHash(blob), stateRoot);
        }

        public bool Verify(byte[] expectedHash, byte[] blob, IHashProvider hashProvider)
        {
            if (blob == null || blob.Length == 0) return true;
            if (expectedHash == null || expectedHash.Length != 32) return true;
            return ByteUtil.AreEqual(hashProvider.ComputeHash(blob), expectedHash);
        }
    }
}
