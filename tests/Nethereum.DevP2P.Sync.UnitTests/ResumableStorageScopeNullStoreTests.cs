using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class ResumableStorageScopeNullStoreTests
    {
        private static readonly byte[] Owner = OwnerHash();

        private static byte[] OwnerHash()
        {
            var o = new byte[32];
            o[0] = 0x5a;
            o[31] = 0x07;
            return o;
        }

        [Fact]
        public void Given_AScopeOpenedWithNoNodeStoreAndAFlatWriter_When_AVerifiedPageIsApplied_Then_SlotsReachFlatTheCursorAdvancesAndNoLocalRootExists()
        {
            var oracle = StorageOracle.Build(24);
            var flat = new RecordingFlatWriter();
            var scope = ResumableStorageScope.Open(null, Owner, flatWriter: flat);

            var (keys, values, proof) = oracle.Page(3, 11);
            var result = scope.ApplyVerifiedPage(oracle.Root, keys[0], keys, values, proof);

            Assert.True(result.Accepted);
            Assert.True(result.HasMore);
            Assert.Equal(SnapHashRanges.IncrementHash(keys[^1]).ToHex(), result.Cursor.ToHex());
            Assert.Equal(result.Cursor.ToHex(), scope.Cursor.ToHex());
            Assert.Equal(keys.Count, flat.Storage.Count);
            foreach (var (key, _, raw) in oracle.Entries.Skip(3).Take(9))
                Assert.Equal(raw.ToHex(), flat.StorageOf(Owner, key).ToHex());

            Assert.Null(scope.CurrentRootHash);
            Assert.Throws<InvalidOperationException>(() => scope.Get(keys[0]));
            scope.CommitDirtyNodes();
        }

        [Fact]
        public void Given_AScopeOpenedWithNoNodeStore_When_APageFailingItsProofIsApplied_Then_NothingReachesFlatAndTheCursorStays()
        {
            var oracle = StorageOracle.Build(24);
            var flat = new RecordingFlatWriter();
            var scope = ResumableStorageScope.Open(null, Owner, flatWriter: flat);

            var (keys, values, proof) = oracle.Page(3, 11);
            values[4] = Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xEE, 0xEE });
            var result = scope.ApplyVerifiedPage(oracle.Root, keys[0], keys, values, proof);

            Assert.False(result.Accepted);
            Assert.Null(scope.Cursor);
            Assert.Empty(flat.Storage);
        }

        [Fact]
        public void Given_NoNodeStoreAndNoFlatWriter_When_AScopeIsOpened_Then_ItThrows()
        {
            Assert.Throws<ArgumentNullException>(() => ResumableStorageScope.Open(null, Owner));
        }

        [Fact]
        public void Given_AScopeOpenedWithANodeStore_When_AVerifiedPageIsApplied_Then_TheLocalRootIsTheTrieOfThatPage()
        {
            var oracle = StorageOracle.Build(24);
            var scope = ResumableStorageScope.Open(new InMemoryContentNodeStore(), Owner, flatWriter: new RecordingFlatWriter());

            var (keys, values, proof) = oracle.Page(0, 23);
            var result = scope.ApplyVerifiedPage(oracle.Root, keys[0], keys, values, proof);

            Assert.True(result.Accepted);
            Assert.Equal(oracle.Root.ToHex(), scope.CurrentRootHash.ToHex());
            Assert.Equal(values[5].ToHex(), scope.Get(keys[5]).ToHex());
        }

        private sealed class StorageOracle
        {
            private readonly PatriciaTrie _trie;
            private readonly InMemoryContentNodeStore _storage;

            private StorageOracle(PatriciaTrie trie, InMemoryContentNodeStore storage, List<(byte[] Key, byte[] Rlp, byte[] Raw)> entries)
            {
                _trie = trie;
                _storage = storage;
                Entries = entries;
            }

            public List<(byte[] Key, byte[] Rlp, byte[] Raw)> Entries { get; }

            public byte[] Root => _trie.Root.GetHash();

            public static StorageOracle Build(int count)
            {
                var keccak = new Sha3Keccack();
                var storage = new InMemoryContentNodeStore();
                var trie = new PatriciaTrie(storage);
                var entries = new List<(byte[] Key, byte[] Rlp, byte[] Raw)>();
                for (int i = 0; i < count; i++)
                {
                    var key = keccak.CalculateHash(new[] { (byte)0x31, (byte)i });
                    var raw = new byte[] { (byte)(i + 1), 0xAB };
                    var rlp = Nethereum.RLP.RLP.EncodeElement(raw);
                    entries.Add((key, rlp, raw));
                    trie.Put(key, rlp);
                }
                trie.SaveDirtyNodesToStorage();
                entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.Key, b.Key));
                return new StorageOracle(trie, storage, entries);
            }

            public (List<byte[]> Keys, List<byte[]> Values, List<byte[]> Proof) Page(int from, int to)
            {
                var slice = Entries.Skip(from).Take(to - from + 1).ToList();
                var keys = slice.Select(e => e.Key).ToList();
                var values = slice.Select(e => e.Rlp).ToList();
                var proof = PatriciaRangeProofGenerator.GenerateProof(_trie.Root, _storage, keys[0], keys[^1]);
                return (keys, values, proof);
            }
        }

        private sealed class RecordingFlatWriter : ISnapFlatStateWriter
        {
            private readonly object _gate = new();
            public readonly Dictionary<string, byte[]> Storage = new();

            public byte[] StorageOf(byte[] accountHash, byte[] slotHash)
            {
                lock (_gate) return Storage.TryGetValue(accountHash.ToHex() + ":" + slotHash.ToHex(), out var v) ? v : null;
            }

            public Task<Account> GetAccountByHashAsync(byte[] accountHash) => throw new NotSupportedException();

            public Task SaveAccountByHashAsync(byte[] accountHash, Account account) => throw new NotSupportedException();

            public Task DeleteAccountByHashAsync(byte[] accountHash) => throw new NotSupportedException();

            public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
            {
                lock (_gate) Storage[accountHash.ToHex() + ":" + slotKeccak.ToHex()] = value;
                return Task.CompletedTask;
            }
        }
    }
}
