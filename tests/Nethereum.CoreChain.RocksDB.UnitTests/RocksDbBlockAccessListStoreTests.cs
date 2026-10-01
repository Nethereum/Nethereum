using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    /// <summary>
    /// AMS-7928-06: the durable half of the EIP-7928 retention rule quoted on
    /// <see cref="IBlockAccessListStore"/>. The store holds the RLP exactly as
    /// <see cref="BlockAccessListRLPEncoder"/> produced it, so what is read back is the pre-image of the
    /// header's <c>block_access_list_hash</c> — never a re-encoding of a decoded list.
    ///
    /// <para>Access lists get the same tiering as every other per-block body artifact: hot window plus
    /// history, composed behind one seam in promotion mode and the history store alone otherwise. These
    /// tests exercise the bundle's seam, in every mode the bundle can be opened in.</para>
    /// </summary>
    public class RocksDbBlockAccessListStoreTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"necc-bal-{Guid.NewGuid():N}");

        public RocksDbBlockAccessListStoreTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private string SubDir(string name)
        {
            var path = Path.Combine(_root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        [Fact]
        public async Task Given_ARetainedAccessList_When_ReadBackByBlockHash_Then_TheBytesAreIdentical()
        {
            using var bundle = RocksDbChainStoreBundle.Open(SubDir("single-roundtrip"));
            var block = await PersistBlockAsync(bundle, 7);
            var encoded = EncodedList("0x0000000000000000000000000000000000000001");

            await bundle.BlockAccessLists.SaveAsync(block.Hash, encoded);

            Assert.Equal(encoded, await bundle.BlockAccessLists.GetByBlockHashAsync(block.Hash));
        }

        [Fact]
        public async Task Given_ARetainedAccessList_When_ADifferentBlockIsRead_Then_ItIsAbsent()
        {
            using var bundle = RocksDbChainStoreBundle.Open(SubDir("single-otherblock"));
            var retained = await PersistBlockAsync(bundle, 7);
            var other = await PersistBlockAsync(bundle, 8);
            await bundle.BlockAccessLists.SaveAsync(retained.Hash, EncodedList("0x0000000000000000000000000000000000000001"));

            Assert.Null(await bundle.BlockAccessLists.GetByBlockHashAsync(other.Hash));
            Assert.Null(await bundle.BlockAccessLists.GetByBlockNumberAsync(8));
        }

        // EIP-7928 gives "no list" and "a list that is empty" different wire values — null against 0xc0.
        [Fact]
        public async Task Given_ABlockThatChangedNothing_When_Retained_Then_TheEmptyListIsNotAbsence()
        {
            using var bundle = RocksDbChainStoreBundle.Open(SubDir("single-empty"));
            var changedNothing = await PersistBlockAsync(bundle, 3);
            var neverRetained = await PersistBlockAsync(bundle, 4);
            var empty = BlockAccessListRLPEncoder.Current.Encode(new List<AccountChanges>());

            await bundle.BlockAccessLists.SaveAsync(changedNothing.Hash, empty);

            var readBack = await bundle.BlockAccessLists.GetByBlockHashAsync(changedNothing.Hash);
            Assert.Equal(RLP.RLP.EncodeList(), readBack);
            Assert.Equal(new byte[] { 0xc0 }, readBack);
            Assert.Null(await bundle.BlockAccessLists.GetByBlockHashAsync(neverRetained.Hash));
        }

        [Fact]
        public async Task Given_ARetainedAccessList_When_ReadBackByBlockNumber_Then_TheBytesAreIdentical()
        {
            using var bundle = RocksDbChainStoreBundle.Open(SubDir("single-bynumber"));
            var block = await PersistBlockAsync(bundle, 17);
            var encoded = EncodedList("0x0000000000000000000000000000000000000002");

            await bundle.BlockAccessLists.SaveAsync(block.Hash, encoded);

            Assert.Equal(encoded, await bundle.BlockAccessLists.GetByBlockNumberAsync(17));
            Assert.Null(await bundle.BlockAccessLists.GetByBlockNumberAsync(18));
        }

        [Fact]
        public async Task Given_ABlockWhoseHeaderWasNeverPersisted_When_ItsAccessListIsRetained_Then_TheStoreRefuses()
        {
            using var bundle = RocksDbChainStoreBundle.Open(SubDir("single-noheader"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => bundle.BlockAccessLists.SaveAsync(Fill32(99), EncodedList("0x0000000000000000000000000000000000000003")));
        }

        [Fact]
        public async Task Given_ARetainedAccessList_When_TheBlockIsOrphaned_Then_TheListIsReclaimed()
        {
            using var bundle = RocksDbChainStoreBundle.Open(SubDir("single-delete"));
            var orphaned = await PersistBlockAsync(bundle, 12);
            var survivor = await PersistBlockAsync(bundle, 11);
            await bundle.BlockAccessLists.SaveAsync(orphaned.Hash, EncodedList("0x0000000000000000000000000000000000000004"));
            await bundle.BlockAccessLists.SaveAsync(survivor.Hash, EncodedList("0x0000000000000000000000000000000000000005"));
            Assert.NotNull(await bundle.BlockAccessLists.GetByBlockNumberAsync(12));

            await bundle.BlockAccessLists.DeleteByBlockNumberAsync(12);

            Assert.Null(await bundle.BlockAccessLists.GetByBlockNumberAsync(12));
            Assert.NotNull(await bundle.BlockAccessLists.GetByBlockNumberAsync(11));
        }

        [Fact]
        public async Task Given_ASplitStore_When_AnAccessListIsRetained_Then_ItResolvesFromTheHistoryDatabase()
        {
            var dir = SubDir("split");
            var encoded = EncodedList("0x0000000000000000000000000000000000000006");
            byte[] hash;

            using (var bundle = RocksDbChainStoreBundle.Open(dir,
                storageOptions: new RocksDbStorageOptions { SplitHistoryStore = true }))
            {
                var block = await PersistBlockAsync(bundle, 21);
                hash = block.Hash;
                await bundle.BlockAccessLists.SaveAsync(hash, encoded);
                Assert.Equal(encoded, await bundle.BlockAccessLists.GetByBlockHashAsync(hash));
            }

            using (var reopened = RocksDbChainStoreBundle.Open(dir,
                storageOptions: new RocksDbStorageOptions { SplitHistoryStore = true }))
            {
                Assert.Equal(encoded, await reopened.BlockAccessLists.GetByBlockHashAsync(hash));
                Assert.Equal(encoded, await reopened.BlockAccessLists.GetByBlockNumberAsync(21));
            }
        }

        [Fact]
        public async Task Given_ARetainedAccessList_When_ItsBlockIsPromoted_Then_TheSameBytesAreServedFromHistory()
        {
            var dir = SubDir("promote");
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var bundle = OpenPromotionBundle(manager, dir, maxHistoryBlocks: 2);
            var encoded = EncodedList("0x0000000000000000000000000000000000000007");
            var blocks = await PersistBlocksAsync(bundle, 1, 5);
            await bundle.BlockAccessLists.SaveAsync(blocks[2].Hash, encoded);

            Assert.True(HotWindowOf(bundle).ContainsBlock(2), "precondition: block 2 is still hot");
            Assert.Equal(encoded, await bundle.BlockAccessLists.GetByBlockNumberAsync(2));

            PromotionServiceOf(bundle).PromoteDurableBlocks(durableHead: 4);

            Assert.False(HotWindowOf(bundle).ContainsBlock(2), "block 2 must have moved out of the hot window");
            Assert.Equal(encoded, await bundle.BlockAccessLists.GetByBlockHashAsync(blocks[2].Hash));
            Assert.Equal(encoded, await bundle.BlockAccessLists.GetByBlockNumberAsync(2));
        }

        [Fact]
        public async Task Given_APromotedAccessList_When_EitherTierIsAskedAlone_Then_OnlyHistoryHasIt()
        {
            var dir = SubDir("promote-tiers");
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var bundle = OpenPromotionBundle(manager, dir, maxHistoryBlocks: 2);
            var encoded = EncodedList("0x0000000000000000000000000000000000000008");
            await PersistBlocksAsync(bundle, 1, 5);
            await bundle.BlockAccessLists.SaveAsync(Fill32(2), encoded);

            var hotOnly = new RocksDbBlockAccessListStore(manager,
                blockAccessListCf: RocksDbManager.CF_HOT_BLOCK_ACCESS_LIST,
                blockHashIndexCf: RocksDbManager.CF_HOT_BLOCK_HASH_INDEX);
            var historyOnly = new RocksDbBlockAccessListStore(manager);
            Assert.Equal(encoded, await hotOnly.GetByBlockNumberAsync(2));
            Assert.Null(await historyOnly.GetByBlockNumberAsync(2));

            PromotionServiceOf(bundle).PromoteDurableBlocks(durableHead: 4);

            Assert.Null(await hotOnly.GetByBlockNumberAsync(2));
            Assert.Equal(encoded, await historyOnly.GetByBlockNumberAsync(2));
        }

        [Fact]
        public async Task Given_AHotAccessList_When_TheBlockIsOrphaned_Then_TheHotRowIsReclaimed()
        {
            var dir = SubDir("promote-delete");
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var bundle = OpenPromotionBundle(manager, dir, maxHistoryBlocks: 2);
            await PersistBlocksAsync(bundle, 1, 5);
            await bundle.BlockAccessLists.SaveAsync(Fill32(5), EncodedList("0x0000000000000000000000000000000000000009"));
            await bundle.BlockAccessLists.SaveAsync(Fill32(4), EncodedList("0x000000000000000000000000000000000000000a"));

            await bundle.BlockAccessLists.DeleteByBlockNumberAsync(5);

            Assert.Null(await bundle.BlockAccessLists.GetByBlockNumberAsync(5));
            Assert.NotNull(await bundle.BlockAccessLists.GetByBlockNumberAsync(4));
        }

        private static RocksDbChainStoreBundle OpenPromotionBundle(RocksDbManager manager, string dir, ulong maxHistoryBlocks)
            => RocksDbChainStoreBundle.FromManager(manager, dir,
                new HistoricalStateOptions { MaxHistoryBlocks = (BigInteger)maxHistoryBlocks }, ownsManager: false);

        private static RocksDbPromotionService PromotionServiceOf(RocksDbChainStoreBundle bundle)
            => bundle.PromotionService;

        private static RocksDbHotBlockWindowStore HotWindowOf(RocksDbChainStoreBundle bundle)
            => bundle.HotWindow;

        private sealed class PersistedBlock
        {
            public byte[] Hash;
            public BigInteger Number;
        }

        private static async Task<Dictionary<long, PersistedBlock>> PersistBlocksAsync(
            RocksDbChainStoreBundle bundle, long from, long to)
        {
            var result = new Dictionary<long, PersistedBlock>();
            for (var n = from; n <= to; n++) result[n] = await PersistBlockAsync(bundle, n);
            return result;
        }

        private static async Task<PersistedBlock> PersistBlockAsync(RocksDbChainStoreBundle bundle, long number)
        {
            var hash = Fill32(number);
            await bundle.Blocks.SaveAsync(new BlockHeader
            {
                BlockNumber = new EvmUInt256((ulong)number),
                ParentHash = Fill32(number - 1),
                TransactionsHash = new byte[32],
                UnclesHash = new byte[32],
                ReceiptHash = new byte[32],
                StateRoot = new byte[32],
                Difficulty = new EvmUInt256(1UL),
                GasLimit = 1,
                Timestamp = 1000 + number,
                ExtraData = Array.Empty<byte>(),
                MixHash = hash,
                Nonce = new byte[8],
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
            }, hash).ConfigureAwait(false);
            return new PersistedBlock { Hash = hash, Number = number };
        }

        private static byte[] Fill32(long seed)
        {
            var h = new byte[32];
            var v = (ulong)seed;
            for (var i = 0; i < 8; i++) h[31 - i] = (byte)(v >> (8 * i));
            return h;
        }

        private static byte[] EncodedList(params string[] addresses)
        {
            var accounts = new List<AccountChanges>();
            foreach (var address in addresses) accounts.Add(new AccountChanges { Address = address });
            return BlockAccessListRLPEncoder.Current.Encode(accounts);
        }
    }
}
