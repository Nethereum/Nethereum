using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests.SecurityAudit71.S12
{
    public class PatriciaProofSoundnessTests
    {
        private static readonly Sha3Keccack Keccak = new Sha3Keccack();

        private class Fixture
        {
            public PatriciaTrie Trie;
            public InMemoryContentNodeStore Store;
            public List<(byte[] Key, byte[] Value)> Entries;
            public byte[] Root;
        }

        private static Fixture Build(int count, int valueLength = 40)
        {
            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store);
            var entries = new List<(byte[], byte[])>();
            for (int i = 0; i < count; i++)
            {
                var key = Keccak.CalculateHash(new[] { (byte)(i >> 8), (byte)i });
                var value = Enumerable.Repeat((byte)(i + 1), valueLength).ToArray();
                entries.Add((key, value));
                trie.Put(key, value);
            }
            trie.SaveDirtyNodesToStorage();
            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.Item1, b.Item1));
            return new Fixture { Trie = trie, Store = store, Entries = entries, Root = trie.Root.GetHash() };
        }

        private static IRangeProofVerifier Range => Nethereum.Merkle.Patricia.ProofVerification.ProofVerification.Current.Range;

        private static (List<byte[]> keys, List<byte[]> values) Slice(Fixture f, int from, int to)
            => (f.Entries.Skip(from).Take(to - from + 1).Select(e => e.Key).ToList(),
                f.Entries.Skip(from).Take(to - from + 1).Select(e => e.Value).ToList());

        [Fact]
        [Trait("Rule", "S12-10")]
        public void Given_a_valid_inclusion_proof_when_the_last_node_is_dropped_then_it_is_not_accepted_as_exclusion()
        {
            var f = Build(64);
            foreach (var e in f.Entries.Take(20))
            {
                var proof = ProofGenerator.GenerateProof(f.Trie, e.Key);
                var truncated = proof.Take(proof.Count - 1).ToList();
                var ok = PatriciaProofVerifier.TryVerify(f.Root, e.Key, truncated, out var v);
                Assert.False(ok && v == null);
            }
        }

        [Fact]
        [Trait("Rule", "S12-10")]
        public void Given_a_proof_for_key_A_when_used_for_present_key_B_then_it_is_not_accepted_as_exclusion()
        {
            var f = Build(64);
            for (int i = 0; i < 20; i++)
            {
                var a = f.Entries[i].Key;
                var b = f.Entries[i + 20].Key;
                var proof = ProofGenerator.GenerateProof(f.Trie, a);
                var ok = PatriciaProofVerifier.TryVerify(f.Root, b, proof, out var v);
                Assert.False(ok && v == null);
            }
        }

        [Fact]
        [Trait("Rule", "S12-10")]
        public void Given_a_valid_proof_when_nodes_are_reordered_duplicated_and_padded_then_the_verdict_is_unchanged()
        {
            var f = Build(64);
            var e = f.Entries[7];
            var proof = ProofGenerator.GenerateProof(f.Trie, e.Key);
            var other = ProofGenerator.GenerateProof(f.Trie, f.Entries[40].Key);
            var mangled = proof.AsEnumerable().Reverse().Concat(proof).Concat(other).ToList();

            Assert.True(PatriciaProofVerifier.TryVerify(f.Root, e.Key, mangled, out var v));
            Assert.Equal(e.Value, v);
        }

        [Fact]
        [Trait("Rule", "S12-10")]
        public void Given_a_proof_when_the_root_is_wrong_then_it_is_rejected()
        {
            var f = Build(64);
            var e = f.Entries[3];
            var proof = ProofGenerator.GenerateProof(f.Trie, e.Key);
            var wrong = (byte[])f.Root.Clone();
            wrong[0] ^= 1;
            Assert.False(PatriciaProofVerifier.TryVerify(wrong, e.Key, proof, out _));
        }

        [Fact]
        [Trait("Rule", "S12-10")]
        public void Given_a_non_empty_root_when_the_proof_is_empty_then_it_is_rejected()
        {
            var f = Build(8);
            Assert.False(PatriciaProofVerifier.TryVerify(f.Root, f.Entries[0].Key, new List<byte[]>(), out _));
        }

        [Fact]
        [Trait("Rule", "S12-11")]
        public void Given_a_range_proof_when_a_middle_key_is_omitted_then_it_is_rejected()
        {
            var f = Build(128);
            var (keys, values) = Slice(f, 20, 60);
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);
            keys.RemoveAt(15); values.RemoveAt(15);
            Assert.False(Range.Verify(f.Root, keys[0], keys, values, proof).Valid);
        }

        [Fact]
        [Trait("Rule", "S12-11")]
        public void Given_a_range_proof_when_a_value_is_altered_then_it_is_rejected()
        {
            var f = Build(128);
            var (keys, values) = Slice(f, 20, 60);
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);
            values[5] = (byte[])values[5].Clone();
            values[5][0] ^= 1;
            Assert.False(Range.Verify(f.Root, keys[0], keys, values, proof).Valid);
        }

        [Fact]
        [Trait("Rule", "S12-12")]
        public void Given_a_range_proof_when_the_first_key_is_dropped_then_it_is_rejected()
        {
            var f = Build(128);
            var (keys, values) = Slice(f, 20, 60);
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);
            var origin = keys[0];
            keys.RemoveAt(0); values.RemoveAt(0);
            Assert.False(Range.Verify(f.Root, origin, keys, values, proof).Valid);
        }

        [Fact]
        [Trait("Rule", "S12-12")]
        public void Given_a_range_proof_when_the_last_key_is_dropped_then_it_is_rejected()
        {
            var f = Build(128);
            var (keys, values) = Slice(f, 20, 60);
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);
            keys.RemoveAt(keys.Count - 1); values.RemoveAt(values.Count - 1);
            Assert.False(Range.Verify(f.Root, keys[0], keys, values, proof).Valid);
        }

        [Fact]
        [Trait("Rule", "S12-13")]
        public void Given_a_range_proof_when_a_key_below_the_requested_origin_is_prepended_then_it_is_rejected_without_throwing()
        {
            var f = Build(128);
            var (keys, values) = Slice(f, 20, 60);
            var origin = keys[0];
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, origin, keys[keys.Count - 1]);
            keys.Insert(0, f.Entries[10].Key); values.Insert(0, f.Entries[10].Value);
            RangeProofResult r = default;
            var ex = Record.Exception(() => r = Range.Verify(f.Root, origin, keys, values, proof));
            Assert.Null(ex);
            Assert.False(r.Valid);
        }

        [Fact]
        [Trait("Rule", "S12-13")]
        public void Given_a_range_proof_when_a_key_above_the_proven_last_key_is_appended_then_it_is_rejected_without_throwing()
        {
            var f = Build(128);
            var (keys, values) = Slice(f, 20, 60);
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);
            keys.Add(f.Entries[90].Key); values.Add(f.Entries[90].Value);
            RangeProofResult r = default;
            var ex = Record.Exception(() => r = Range.Verify(f.Root, keys[0], keys, values, proof));
            Assert.Null(ex);
            Assert.False(r.Valid);
        }

        [Fact]
        [Trait("Rule", "S12-14")]
        public void Given_a_range_proof_when_keys_are_unsorted_or_duplicated_then_it_is_rejected()
        {
            var f = Build(128);
            var (keys, values) = Slice(f, 20, 40);
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);
            var k2 = keys.ToList(); var v2 = values.ToList();
            (k2[3], k2[4]) = (k2[4], k2[3]); (v2[3], v2[4]) = (v2[4], v2[3]);
            Assert.False(Range.Verify(f.Root, keys[0], k2, v2, proof).Valid);
            var k3 = keys.ToList(); var v3 = values.ToList();
            k3.Insert(4, k3[3]); v3.Insert(4, v3[3]);
            Assert.False(Range.Verify(f.Root, keys[0], k3, v3, proof).Valid);
        }

        [Fact]
        [Trait("Rule", "S12-15")]
        public void Given_a_non_empty_trie_when_an_empty_range_is_claimed_with_a_proof_of_a_present_key_then_it_is_rejected()
        {
            var f = Build(64);
            var origin = f.Entries[10].Key;
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, origin);
            Assert.False(Range.Verify(f.Root, origin, new List<byte[]>(), new List<byte[]>(), proof).Valid);
        }

        [Fact]
        [Trait("Rule", "S12-15")]
        public void Given_a_non_empty_trie_when_an_empty_range_is_claimed_before_existing_keys_then_it_is_rejected()
        {
            var f = Build(64);
            var origin = new byte[32];
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, origin);
            Assert.False(Range.Verify(f.Root, origin, new List<byte[]>(), new List<byte[]>(), proof).Valid);
        }

        [Fact]
        [Trait("Rule", "S12-15")]
        public void Given_a_non_empty_trie_when_an_empty_range_is_claimed_without_any_proof_then_it_is_rejected()
        {
            var f = Build(64);
            Assert.False(Range.Verify(f.Root, new byte[32], new List<byte[]>(), new List<byte[]>(), null).Valid);
            Assert.False(Range.Verify(f.Root, new byte[32], new List<byte[]>(), new List<byte[]>(), new List<byte[]>()).Valid);
        }

        [Fact]
        [Trait("Rule", "S12-16")]
        public void Given_a_range_proof_that_ends_before_the_trie_end_then_has_more_is_true()
        {
            var f = Build(128);
            var (keys, values) = Slice(f, 20, 60);
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);
            var r = Range.Verify(f.Root, keys[0], keys, values, proof);
            Assert.True(r.Valid);
            Assert.True(r.HasMore);
        }

        [Fact]
        [Trait("Rule", "S12-16")]
        public void Given_a_range_proof_with_only_the_left_edge_then_a_partial_range_is_rejected()
        {
            var f = Build(128);
            var (keys, values) = Slice(f, 20, 60);
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0]);
            var r = Range.Verify(f.Root, keys[0], keys, values, proof);
            Assert.False(r.Valid);
        }

        [Fact]
        [Trait("Rule", "S12-17")]
        public void Given_a_single_key_range_when_the_value_is_forged_then_it_is_rejected()
        {
            var f = Build(128);
            var k = f.Entries[30].Key;
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, k);
            var r = Range.Verify(f.Root, k, new List<byte[]> { k }, new List<byte[]> { new byte[] { 9, 9 } }, proof);
            Assert.False(r.Valid);
        }

        [Fact]
        [Trait("Rule", "S12-18")]
        public void Given_a_range_proof_of_garbage_nodes_then_it_is_rejected_without_throwing()
        {
            var f = Build(64);
            var (keys, values) = Slice(f, 5, 30);
            var garbage = new List<byte[]> { new byte[] { 0xC0 }, new byte[] { 0xFF, 0xFF }, new byte[0], new byte[100] };
            RangeProofResult r = default;
            var ex = Record.Exception(() => r = Range.Verify(f.Root, keys[0], keys, values, garbage));
            Assert.Null(ex);
            Assert.False(r.Valid);
        }

        [Fact]
        [Trait("Rule", "S12-18")]
        public void Given_a_range_proof_with_a_short_middle_key_then_it_is_rejected_without_throwing()
        {
            var f = Build(64);
            var (keys, values) = Slice(f, 5, 30);
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);
            keys[3] = keys[3].Take(16).ToArray();
            RangeProofResult r = default;
            var ex = Record.Exception(() => r = Range.Verify(f.Root, keys[0], keys, values, proof));
            Assert.Null(ex);
            Assert.False(r.Valid);
        }

        [Fact]
        [Trait("Rule", "S12-18")]
        public void Given_a_range_proof_with_an_empty_first_key_then_it_is_rejected_without_throwing()
        {
            var f = Build(64);
            var (keys, values) = Slice(f, 5, 30);
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);
            RangeProofResult r = default;
            var ex = Record.Exception(() => r = Range.Verify(f.Root, new byte[0], keys, values, proof));
            Assert.Null(ex);
        }

        [Fact]
        [Trait("Rule", "S12-19")]
        public void Given_no_proof_when_keys_are_a_strict_subset_of_the_trie_then_it_is_rejected()
        {
            var f = Build(40);
            var (keys, values) = Slice(f, 0, 38);
            Assert.False(Range.Verify(f.Root, keys[0], keys, values, null).Valid);
        }

        [Fact]
        [Trait("Rule", "S12-20")]
        public void Given_an_entry_proof_when_the_value_or_key_differs_then_verify_entry_rejects()
        {
            var f = Build(64);
            var e = f.Entries[9];
            var proof = ProofGenerator.GenerateProof(f.Trie, e.Key);
            Assert.True(Range.VerifyEntry(f.Root, e.Key, e.Value, proof));
            Assert.False(Range.VerifyEntry(f.Root, e.Key, new byte[] { 1 }, proof));
            Assert.False(Range.VerifyEntry(f.Root, f.Entries[50].Key, e.Value, proof));
        }
    }
}
