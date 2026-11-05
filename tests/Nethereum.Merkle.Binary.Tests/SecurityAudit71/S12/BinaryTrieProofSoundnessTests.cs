using System;
using System.Linq;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Proofs;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Binary.Tests.SecurityAudit71.S12
{
    public class BinaryTrieProofSoundnessTests
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
            var keys = new[] { Key(0x00, 1), Key(0x80, 2), Key(0x40, 3), Key(0xC0, 4), Key(0x20, 5) };
            for (int i = 0; i < keys.Length; i++) trie.Put(keys[i], Val((byte)(0x10 + i)));
            return (trie, trie.ComputeRoot(), keys);
        }

        [Fact]
        [Trait("Rule", "S12-50")]
        public void Given_a_proof_with_a_malformed_node_when_verified_then_it_returns_null_without_throwing()
        {
            var (trie, root, keys) = Build();
            var proof = new BinaryTrieProver(trie).BuildProof(keys[0]);
            var nodes = proof.Nodes.ToList();
            nodes.Add(new byte[] { 0x7F, 1, 2, 3 });
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);

            byte[] result = new byte[1];
            var ex = Record.Exception(() => result = verifier.VerifyProof(root, keys[0], new BinaryTrieProof { Nodes = nodes.ToArray() }));

            Assert.Null(ex);
            Assert.Equal(Val(0x10), result);
        }

        [Fact]
        [Trait("Rule", "S12-50")]
        public void Given_a_proof_with_a_truncated_stem_node_when_verified_then_it_returns_null_without_throwing()
        {
            var (trie, root, keys) = Build();
            var proof = new BinaryTrieProver(trie).BuildProof(keys[0]);
            var nodes = proof.Nodes.ToList();
            nodes.Add(new byte[] { 1, 0, 0 });
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);

            var ex = Record.Exception(() => verifier.VerifyProof(root, keys[0], new BinaryTrieProof { Nodes = nodes.ToArray() }));

            Assert.Null(ex);
        }

        [Fact]
        [Trait("Rule", "S12-51")]
        public void Given_a_proof_for_key_A_when_used_for_key_B_then_no_value_is_returned()
        {
            var (trie, root, keys) = Build();
            var prover = new BinaryTrieProver(trie);
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var proofA = prover.BuildProof(keys[0]);

            foreach (var other in keys.Skip(1))
                Assert.Null(verifier.VerifyProof(root, other, proofA));
        }

        [Fact]
        [Trait("Rule", "S12-51")]
        public void Given_a_proof_for_a_stem_when_another_slot_of_the_same_stem_is_requested_then_only_its_own_value_is_returned()
        {
            var (trie, root, keys) = Build();
            var proof = new BinaryTrieProver(trie).BuildProof(keys[0]);
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var sibling = (byte[])keys[0].Clone();
            sibling[31] = 9;

            Assert.Null(verifier.VerifyProof(root, sibling, proof));
        }

        [Fact]
        [Trait("Rule", "S12-51")]
        public void Given_a_proof_when_a_node_is_removed_then_it_is_not_accepted()
        {
            var (trie, root, keys) = Build();
            var proof = new BinaryTrieProver(trie).BuildProof(keys[2]);
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);

            for (int drop = 0; drop < proof.Nodes.Length; drop++)
            {
                var nodes = proof.Nodes.Where((_, i) => i != drop).ToArray();
                Assert.Null(verifier.VerifyProof(root, keys[2], new BinaryTrieProof { Nodes = nodes }));
            }
        }

        [Fact]
        [Trait("Rule", "S12-51")]
        public void Given_a_proof_when_a_stem_value_byte_is_flipped_then_it_is_not_accepted()
        {
            var (trie, root, keys) = Build();
            var proof = new BinaryTrieProver(trie).BuildProof(keys[1]);
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var nodes = proof.Nodes.Select(n => (byte[])n.Clone()).ToArray();
            var last = nodes[nodes.Length - 1];
            last[last.Length - 1] ^= 1;

            Assert.Null(verifier.VerifyProof(root, keys[1], new BinaryTrieProof { Nodes = nodes }));
        }

        [Fact]
        [Trait("Rule", "S12-52")]
        public void Given_a_proof_when_the_root_is_wrong_or_zero_then_it_is_rejected()
        {
            var (trie, root, keys) = Build();
            var proof = new BinaryTrieProver(trie).BuildProof(keys[0]);
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var wrong = (byte[])root.Clone();
            wrong[5] ^= 1;

            Assert.Null(verifier.VerifyProof(wrong, keys[0], proof));
            Assert.Null(verifier.VerifyProof(new byte[32], keys[0], proof));
        }

        [Fact]
        [Trait("Rule", "S12-52")]
        public void Given_a_proof_when_reordered_duplicated_and_padded_then_the_verdict_is_unchanged()
        {
            var (trie, root, keys) = Build();
            var prover = new BinaryTrieProver(trie);
            var proof = prover.BuildProof(keys[3]);
            var other = prover.BuildProof(keys[4]);
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var nodes = proof.Nodes.Reverse().Concat(proof.Nodes).Concat(other.Nodes).ToArray();

            Assert.Equal(Val(0x13), verifier.VerifyProof(root, keys[3], new BinaryTrieProof { Nodes = nodes }));
        }

        [Fact]
        [Trait("Rule", "S12-52")]
        public void Given_a_proof_with_null_elements_when_verified_then_it_does_not_throw()
        {
            var (trie, root, keys) = Build();
            var proof = new BinaryTrieProver(trie).BuildProof(keys[0]);
            var nodes = new byte[][] { null }.Concat(proof.Nodes).ToArray();
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);

            var ex = Record.Exception(() => verifier.VerifyProof(root, keys[0], new BinaryTrieProof { Nodes = nodes }));
            Assert.Null(ex);
        }
    }
}
