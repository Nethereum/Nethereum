using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Snap
{
    public class Phase2ExtensionSpanClearingTests : IDisposable
    {
        private static readonly byte[] Owner = Enumerable.Repeat((byte)0x42, 32).ToArray();
        private static readonly byte[][] SpannedPaths = { new byte[] { 0x3 }, new byte[] { 0x3, 0xf } };
        private static readonly byte[] SlotValue = RLP.RLP.EncodeElement(Enumerable.Repeat((byte)0x77, 32).ToArray());

        private readonly string _dbPath;
        private readonly RocksDbManager _manager;
        private readonly RocksDbChainStoreBundle _bundle;

        public Phase2ExtensionSpanClearingTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_phase2_span_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath, PathKeyedState = true });
            _bundle = RocksDbChainStoreBundle.FromManager(_manager, _dbPath, HistoricalStateOptions.Default, ownsManager: false);
        }

        public void Dispose()
        {
            _bundle?.Dispose();
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
                try { Directory.Delete(_dbPath, true); } catch { }
        }

        [Fact]
        public async Task Given_AStorageTrieAlreadyStoredForAnOwner_When_TrieSnapSyncSinkWritesThatOwnersStorageThroughAFreshScopeWhoseExtensionSpansAStoredNode_Then_TheStoredNodeIsDeleted()
        {
            SaveEarlierStorageTrie();
            AssertStoredAt(Owner);
            var sink = new TrieSnapSyncSink(_bundle.StateTrieNodes, _bundle.State);

            var scope = await sink.BeginAccountStorageAsync(Owner, StorageRootOf(ResumedSlots), CancellationToken.None);
            foreach (var slot in ResumedSlots) await scope.WriteSlotAsync(slot, SlotValue, CancellationToken.None);
            await scope.EndAsync(CancellationToken.None);

            AssertAbsentAt(Owner);
        }

        [Fact]
        public void Given_ACursoredWhaleScopeReattachedOverStoredNodesFromAnEarlierWrite_When_ItCommitsAnExtensionSpanningOne_Then_ThatNodeIsDeleted()
        {
            SaveEarlierStorageTrie();
            var extensionWriter = new PatriciaTrie(_bundle.StateTrieNodes, Owner);
            foreach (var slot in ResumedSlots) extensionWriter.Put(slot, SlotValue);
            extensionWriter.SaveDirtyNodesToStorage();
            AssertStoredAt(Owner);
            var completed = ResumedSlots.Append(Key(0x3, 0xf, 0xf, 0xc)).ToList();
            var (oracleRoot, oracleStore) = Oracle(completed);
            var lastSlot = completed[^1];

            var scope = ResumableStorageScope.Open(_bundle.StateTrieNodes, Owner);
            var page = scope.ApplyVerifiedPage(oracleRoot, lastSlot, new List<byte[]> { lastSlot }, new List<byte[]> { SlotValue },
                PatriciaRangeProofGenerator.GenerateProof(PatriciaTrie.LoadFromStorage(oracleRoot, oracleStore).Root, oracleStore, lastSlot, lastSlot));
            scope.CommitDirtyNodes();

            Assert.True(page.Accepted);
            Assert.Equal(oracleRoot, scope.CurrentRootHash, ByteArrayComparer.Current);
            AssertAbsentAt(Owner);
        }

        [Fact]
        public async Task Given_AnAccountTrieStoredByAnEarlierRun_When_ANewTrieSnapSyncSinkWritesAnExtensionSpanningAStoredBranch_Then_TheBranchIsDeleted()
        {
            var earlier = new TrieSnapSyncSink(_bundle.StateTrieNodes, _bundle.State);
            foreach (var account in EarlierKeys()) await earlier.WriteAccountAsync(account, SlimAccount, CancellationToken.None);
            await earlier.FinaliseRootAsync(CancellationToken.None);
            AssertStoredAt(Array.Empty<byte>());

            var resumed = new TrieSnapSyncSink(_bundle.StateTrieNodes, _bundle.State);
            foreach (var account in ResumedSlots) await resumed.WriteAccountAsync(account, SlimAccount, CancellationToken.None);
            await resumed.FinaliseRootAsync(CancellationToken.None);

            AssertAbsentAt(Array.Empty<byte>());
        }

        private static readonly byte[] SlimAccount = SlimAccountEncoder.ToSlim(new AccountEncoder().Encode(new Account
        {
            Nonce = 1,
            Balance = 1000,
            CodeHash = DefaultValues.EMPTY_DATA_HASH,
            StateRoot = DefaultValues.EMPTY_TRIE_HASH,
        }));

        private static readonly List<byte[]> ResumedSlots = new() { Key(0x3, 0xf, 0xf, 0x5), Key(0x3, 0xf, 0xf, 0x9) };

        private static IEnumerable<byte[]> EarlierKeys() => new[]
        {
            Key(0x3, 0x0), Key(0x3, 0xf, 0x0), Key(0x3, 0xf, 0xf, 0x1), Key(0x3, 0xf, 0xf, 0x5), Key(0x3, 0xf, 0xf, 0x9), Key(0x7),
        };

        private void SaveEarlierStorageTrie()
        {
            var earlier = new PatriciaTrie(_bundle.StateTrieNodes, Owner);
            foreach (var slot in EarlierKeys()) earlier.Put(slot, SlotValue);
            earlier.SaveDirtyNodesToStorage();
        }

        private static byte[] StorageRootOf(IEnumerable<byte[]> slots)
        {
            var trie = new PatriciaTrie();
            foreach (var slot in slots) trie.Put(slot, SlotValue);
            return trie.Root.GetHash();
        }

        private static (byte[] Root, InMemoryContentNodeStore Store) Oracle(IEnumerable<byte[]> slots)
        {
            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store);
            foreach (var slot in slots) trie.Put(slot, SlotValue);
            trie.SaveDirtyNodesToStorage();
            return (trie.Root.GetHash(), store);
        }

        private static byte[] Key(params byte[] leadingNibbles)
        {
            var key = new byte[32];
            for (var i = 0; i < 64; i++)
            {
                var nibble = i < leadingNibbles.Length ? leadingNibbles[i] : (byte)0xa;
                key[i / 2] |= (byte)(i % 2 == 0 ? nibble << 4 : nibble);
            }
            return key;
        }

        private byte[] StoredAt(byte[] owner, byte[] path) => ((IRawNodeReader)_bundle.StateTrieNodes).TryGetRawNode(owner, path);

        private void AssertStoredAt(byte[] owner)
            => Assert.All(SpannedPaths, path => Assert.True(StoredAt(owner, path) != null, $"no stored node at [{path.ToHex()}] before the write"));

        private void AssertAbsentAt(byte[] owner)
            => Assert.All(SpannedPaths, path => Assert.True(StoredAt(owner, path) == null, $"[{path.ToHex()}] inside the written extension's span still holds a stored node"));
    }
}
