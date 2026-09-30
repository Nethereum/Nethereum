using System.Collections.Generic;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Nethereum.Documentation;
using Xunit;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Proofs;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class ProofGeneratorRootFirstOrderingTests
    {
        private static (PatriciaTrie trie, InMemoryContentNodeStore storage, List<byte[]> keys) Build(int count)
        {
            var keccak = new Sha3Keccack();
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var keys = new List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                var keyHash = keccak.CalculateHash(new[] { (byte)(i >> 8), (byte)(i & 0xff) });
                keys.Add(keyHash);
                trie.Put(keyHash, new byte[] { (byte)(i & 0xff), 0xAB, 0xCD });
            }
            trie.SaveDirtyNodesToStorage();
            return (trie, storage, keys);
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "state-proofs", "A generated proof is root-first: proof[0] hashes to the trie root", Order = 1)]
        [Fact]
        public void FirstProofNode_HashesTo_StateRoot()
        {
            var (trie, _, keys) = Build(200);
            var rootHash = trie.Root.GetHash();

            var proof = ProofGenerator.GenerateProof(trie, keys[0]);

            Assert.NotNull(proof);
            Assert.NotEmpty(proof);
            Assert.Equal(rootHash, new Sha3Keccack().CalculateHash(proof[0]));
        }

        [Fact]
        public void EveryProofNode_ChainsFromTheRootDownThePath()
        {
            var (trie, _, keys) = Build(200);
            var rootHash = trie.Root.GetHash();
            var hashProvider = new Sha3KeccackHashProvider();

            foreach (var key in new[] { keys[0], keys[50], keys[199] })
            {
                var proof = ProofGenerator.GenerateProof(trie, key);
                Assert.NotNull(proof);
                Assert.NotEmpty(proof);

                Assert.Equal(rootHash, hashProvider.ComputeHash(proof[0]));

                for (int i = 0; i + 1 < proof.Count; i++)
                {
                    var nextHash = hashProvider.ComputeHash(proof[i + 1]);
                    Assert.True(ContainsHashReference(proof[i], nextHash),
                        "proof node " + i + " must reference the next node (root-first path ordering)");
                }
            }
        }

        private static bool ContainsHashReference(byte[] parentRlp, byte[] childHash)
        {
            for (int i = 0; i + childHash.Length <= parentRlp.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < childHash.Length; j++)
                {
                    if (parentRlp[i + j] != childHash[j]) { match = false; break; }
                }
                if (match) return true;
            }
            return false;
        }
    }
}
