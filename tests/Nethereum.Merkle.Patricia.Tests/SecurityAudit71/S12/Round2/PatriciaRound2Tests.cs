using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests.SecurityAudit71.S12.Round2
{
    public class PatriciaRound2Tests
    {
        private static readonly Sha3Keccack Keccak = new Sha3Keccack();

        private class Fixture
        {
            public PatriciaTrie Trie;
            public InMemoryContentNodeStore Store;
            public List<(byte[] Key, byte[] Value)> Entries;
            public byte[] Root;
        }

        private static Fixture Build(int count, int valueLength)
        {
            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store);
            var entries = new List<(byte[], byte[])>();
            for (int i = 0; i < count; i++)
            {
                var key = Keccak.CalculateHash(new[] { (byte)(i >> 8), (byte)i, (byte)7 });
                var value = Enumerable.Repeat((byte)(i + 1), valueLength).ToArray();
                entries.Add((key, value));
                trie.Put(key, value);
            }
            trie.SaveDirtyNodesToStorage();
            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.Item1, b.Item1));
            return new Fixture { Trie = trie, Store = store, Entries = entries, Root = trie.Root.GetHash() };
        }

        private static IRangeProofVerifier Range => Nethereum.Merkle.Patricia.ProofVerification.ProofVerification.Current.Range;

        private static List<byte[]> CloneNodes(IEnumerable<byte[]> nodes) => nodes.Select(n => (byte[])n.Clone()).ToList();

        [Theory]
        [InlineData(40)]
        [InlineData(3)]
        [Trait("Rule", "S12-R2-10")]
        public void Given_a_valid_range_proof_when_proof_bytes_are_fuzzed_then_nothing_throws_and_acceptance_never_changes_the_verdict(int valueLength)
        {
            var f = Build(96, valueLength);
            var keys = f.Entries.Skip(10).Take(50).Select(e => e.Key).ToList();
            var values = f.Entries.Skip(10).Take(50).Select(e => e.Value).ToList();
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);
            var honest = Range.Verify(f.Root, keys[0], keys, values, proof);
            Assert.True(honest.Valid);

            var rng = new Random(1234);
            for (int iter = 0; iter < 1500; iter++)
            {
                var mutated = CloneNodes(proof);
                var node = rng.Next(mutated.Count);
                switch (rng.Next(4))
                {
                    case 0: mutated[node][rng.Next(mutated[node].Length)] ^= (byte)(1 << rng.Next(8)); break;
                    case 1: mutated[node] = mutated[node].Take(rng.Next(mutated[node].Length)).ToArray(); break;
                    case 2: mutated[node] = mutated[node].Concat(new byte[] { (byte)rng.Next(256) }).ToArray(); break;
                    default: mutated.RemoveAt(node); break;
                }

                RangeProofResult r = default;
                var ex = Record.Exception(() => r = Range.Verify(f.Root, keys[0], keys, values, mutated));
                Assert.Null(ex);
                if (r.Valid) Assert.Equal(honest.HasMore, r.HasMore);
            }
        }

        [Fact]
        [Trait("Rule", "S12-R2-11")]
        public void Given_a_valid_range_proof_when_a_key_or_value_byte_is_flipped_then_it_is_always_rejected()
        {
            var f = Build(96, 40);
            var keys = f.Entries.Skip(10).Take(50).Select(e => e.Key).ToList();
            var values = f.Entries.Skip(10).Take(50).Select(e => e.Value).ToList();
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);

            var rng = new Random(99);
            for (int iter = 0; iter < 800; iter++)
            {
                var k = keys.Select(x => (byte[])x.Clone()).ToList();
                var v = values.Select(x => (byte[])x.Clone()).ToList();
                var i = rng.Next(k.Count);
                if (rng.Next(2) == 0) k[i][rng.Next(k[i].Length)] ^= (byte)(1 << rng.Next(8));
                else v[i][rng.Next(v[i].Length)] ^= (byte)(1 << rng.Next(8));

                RangeProofResult r = default;
                var ex = Record.Exception(() => r = Range.Verify(f.Root, keys[0], k, v, proof));
                Assert.Null(ex);
                Assert.False(r.Valid);
            }
        }

        [Fact]
        [Trait("Rule", "S12-R2-12")]
        public void Given_a_present_key_when_the_proof_bytes_are_fuzzed_then_single_key_verification_never_returns_a_different_value_or_an_exclusion()
        {
            var f = Build(96, 40);
            var target = f.Entries[33];
            var proof = ProofGenerator.GenerateProof(f.Trie, target.Key);

            var rng = new Random(77);
            for (int iter = 0; iter < 2000; iter++)
            {
                var mutated = CloneNodes(proof);
                var node = rng.Next(mutated.Count);
                switch (rng.Next(3))
                {
                    case 0: mutated[node][rng.Next(mutated[node].Length)] ^= (byte)(1 << rng.Next(8)); break;
                    case 1: mutated[node] = mutated[node].Take(rng.Next(mutated[node].Length)).ToArray(); break;
                    default: mutated.RemoveAt(node); break;
                }

                byte[] value = null;
                bool ok = false;
                var ex = Record.Exception(() => ok = PatriciaProofVerifier.TryVerify(f.Root, target.Key, mutated, out value));
                Assert.Null(ex);
                Assert.False(ok);
            }
        }

        [Fact]
        [Trait("Rule", "S12-R2-13")]
        public void Given_an_account_proof_enumerable_that_throws_midway_then_the_account_verifier_returns_false()
        {
            var f = Build(8, 40);
            var proof = ProofGenerator.GenerateProof(f.Trie, f.Entries[0].Key);
            IEnumerable<byte[]> Throwing()
            {
                yield return proof[0];
                throw new InvalidOperationException("source failed");
            }

            var storageProof = new List<byte[]> { proof[0] };
            var account = new Account { Nonce = 1 };
            Assert.False(AccountProofVerification.Current.Verify(f.Root, Throwing(), "0x0000000000000000000000000000000000000001", account));
            Assert.False(StorageProofVerification.Current.Verify(f.Root, storageProof, new byte[32], new byte[] { 1 }));
            Assert.False(StorageProofVerification.Current.Verify(f.Root, new List<byte[]> { null }, new byte[32], new byte[] { 1 }));
        }

        [Fact]
        [Trait("Rule", "S12-R2-14")]
        public void Given_null_arguments_the_range_verifier_still_rejects_them_as_before()
        {
            var f = Build(8, 40);
            Assert.Throws<ArgumentNullException>(() => Range.Verify(null, f.Entries[0].Key, new List<byte[]>(), new List<byte[]>(), null));
            Assert.Throws<ArgumentNullException>(() => Range.Verify(f.Root, null, new List<byte[]>(), new List<byte[]>(), null));
        }

        [Fact]
        [Trait("Rule", "S12-R2-15")]
        public void Given_null_entries_inside_the_key_or_value_lists_then_the_range_verifier_returns_invalid()
        {
            var f = Build(40, 40);
            var keys = f.Entries.Skip(5).Take(10).Select(e => e.Key).ToList();
            var values = f.Entries.Skip(5).Take(10).Select(e => e.Value).ToList();
            var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);

            var k2 = keys.ToList(); k2[4] = null;
            var v2 = values.ToList(); v2[4] = null;

            Assert.False(Range.Verify(f.Root, keys[0], k2, values, proof).Valid);
            Assert.False(Range.Verify(f.Root, keys[0], keys, v2, proof).Valid);
        }

        [Fact]
        [Trait("Rule", "S12-R2-16")]
        public void Given_every_pair_of_adjacent_slices_of_a_trie_then_valid_ranges_still_verify_and_report_has_more_honestly()
        {
            var f = Build(70, 12);
            for (int from = 0; from < 60; from += 7)
                for (int len = 1; len <= 9; len++)
                {
                    var to = Math.Min(from + len - 1, f.Entries.Count - 1);
                    var keys = f.Entries.Skip(from).Take(to - from + 1).Select(e => e.Key).ToList();
                    var values = f.Entries.Skip(from).Take(to - from + 1).Select(e => e.Value).ToList();
                    var proof = PatriciaRangeProofGenerator.GenerateProof(f.Trie.Root, f.Store, keys[0], keys[keys.Count - 1]);
                    var r = Range.Verify(f.Root, keys[0], keys, values, proof);
                    Assert.True(r.Valid);
                    Assert.Equal(to < f.Entries.Count - 1, r.HasMore);
                }
        }
    }
}
