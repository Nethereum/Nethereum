using System;
using System.Linq;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Proofs;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Binary.Tests.SecurityAudit71.S12.Round2
{
    public class BinaryTrieRound2Tests
    {
        private static byte[] Key(byte prefix, byte last)
        {
            var k = new byte[32];
            k[0] = prefix;
            k[31] = last;
            return k;
        }

        private static byte[] Val(byte b)
        {
            var v = new byte[32];
            v[0] = b;
            return v;
        }

        private static (BinaryTrie trie, byte[] root, byte[][] keys) Build()
        {
            var trie = new BinaryTrie(new Sha256HashProvider());
            var keys = new[] { Key(0x00, 1), Key(0x80, 2), Key(0x40, 3), Key(0xC0, 4), Key(0x20, 5), Key(0x21, 6) };
            for (int i = 0; i < keys.Length; i++) trie.Put(keys[i], Val((byte)(0x10 + i)));
            return (trie, trie.ComputeRoot(), keys);
        }

        [Fact]
        [Trait("Rule", "S12-R2-20")]
        public void Given_a_proof_when_any_single_node_is_replaced_by_an_undecodable_blob_then_the_result_is_null_never_another_value()
        {
            var (trie, root, keys) = Build();
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var prover = new BinaryTrieProver(trie);

            foreach (var key in keys)
            {
                var proof = prover.BuildProof(key);
                for (int i = 0; i < proof.Nodes.Length; i++)
                {
                    foreach (var junk in new[] { new byte[] { 0x7F, 1 }, new byte[] { 1, 0, 0 }, new byte[] { 2, 1, 2, 3 }, new byte[300] })
                    {
                        var nodes = proof.Nodes.Select(n => (byte[])n.Clone()).ToArray();
                        nodes[i] = junk;
                        byte[] result = new byte[1];
                        var ex = Record.Exception(() => result = verifier.VerifyProof(root, key, new BinaryTrieProof { Nodes = nodes }));
                        Assert.Null(ex);
                        Assert.Null(result);
                    }
                }
            }
        }

        [Fact]
        [Trait("Rule", "S12-R2-21")]
        public void Given_a_proof_when_nodes_are_fuzzed_then_the_verdict_is_the_honest_value_or_null()
        {
            var (trie, root, keys) = Build();
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var prover = new BinaryTrieProver(trie);
            var rng = new Random(5);

            for (int iter = 0; iter < 3000; iter++)
            {
                var ki = rng.Next(keys.Length);
                var proof = prover.BuildProof(keys[ki]);
                var nodes = proof.Nodes.Select(n => (byte[])n.Clone()).ToList();
                var node = rng.Next(nodes.Count);
                switch (rng.Next(4))
                {
                    case 0: nodes[node][rng.Next(nodes[node].Length)] ^= (byte)(1 << rng.Next(8)); break;
                    case 1: nodes[node] = nodes[node].Take(rng.Next(nodes[node].Length)).ToArray(); break;
                    case 2: nodes[node] = nodes[node].Concat(new byte[] { (byte)rng.Next(256) }).ToArray(); break;
                    default: nodes.RemoveAt(node); break;
                }

                byte[] result = null;
                var ex = Record.Exception(() => result = verifier.VerifyProof(root, keys[ki], new BinaryTrieProof { Nodes = nodes.ToArray() }));
                Assert.Null(ex);
                if (result != null) Assert.Equal(Val((byte)(0x10 + ki)), result);
            }
        }

        [Fact]
        [Trait("Rule", "S12-R2-22")]
        public void Given_a_conflicting_stem_node_for_the_same_stem_with_other_values_then_it_cannot_displace_the_committed_node()
        {
            var (trie, root, keys) = Build();
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var other = new BinaryTrie(new Sha256HashProvider());
            other.Put(keys[0], Val(0xEE));
            var conflicting = new BinaryTrieProver(other).BuildProof(keys[0]);
            var honest = new BinaryTrieProver(trie).BuildProof(keys[0]);

            var combined = conflicting.Nodes.Concat(honest.Nodes).ToArray();
            Assert.Equal(Val(0x10), verifier.VerifyProof(root, keys[0], new BinaryTrieProof { Nodes = combined }));

            var combinedReversed = honest.Nodes.Concat(conflicting.Nodes).ToArray();
            Assert.Equal(Val(0x10), verifier.VerifyProof(root, keys[0], new BinaryTrieProof { Nodes = combinedReversed }));
        }
    }
}
