using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class IndexBackedHistoricalSnapTests : IDisposable
    {
        private const string TargetAddr = "0x0000000000000000000000000000000000000001";
        private static readonly BigInteger BalanceAtN = 100;
        private static readonly BigInteger BalanceAtHead = 999;

        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();

        public IndexBackedHistoricalSnapTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-c2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
        }

        private sealed class JournalingStore : ITrieNodeStore
        {
            private readonly RocksDbPathTrieNodeStore _path;
            private readonly RocksDbNodeReverseDiffStore _journal;
            public ulong Block;
            public JournalingStore(RocksDbPathTrieNodeStore path, RocksDbNodeReverseDiffStore journal)
            { _path = path; _journal = journal; }
            public void Commit(TrieNodeSet set) => _journal.RecordAndCommit(Block, _path, set);
            public byte[] Get(Node r) => _path.Get(r);
            public bool Contains(Node r) => _path.Contains(r);
            public bool ContainsKey(byte[] r) => _path.ContainsKey(r);
            public void Flush() => _path.Flush();
            public void Clear() => _path.Clear();
        }

        private static byte[] AddrHash(string address)
            => _keccak.CalculateHash(AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray());

        private static byte[] EncodeEoa(BigInteger balance, BigInteger nonce)
            => AccountEncoder.Current.Encode(new Account
            {
                Nonce = nonce,
                Balance = balance,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH
            });

        private static string Filler(int i) => "0x" + i.ToString("x2").PadLeft(40, '0');

        private (RocksDbPathTrieNodeStore path, RocksDbNodeReverseDiffStore journal, byte[] root1, byte[] rootAsOfN, byte[] rootHead)
            JournalThreeBlocks()
        {
            var pathStore = new RocksDbPathTrieNodeStore(_mgr);
            var journal = new RocksDbNodeReverseDiffStore(_mgr, buildKeyMajorIndex: true);
            var shim = new JournalingStore(pathStore, journal);
            var account = new PatriciaTrie(shim, _hp) { Tracer = new TrieTracer() };

            shim.Block = 1;
            account.Put(AddrHash(TargetAddr), EncodeEoa(BalanceAtN, nonce: 1));
            for (int i = 2; i <= 10; i++) account.Put(AddrHash(Filler(i)), EncodeEoa(balance: i, nonce: 0));
            account.SaveDirtyNodesToStorage();
            var root1 = account.Root.GetHash();

            shim.Block = 2;
            account.Put(AddrHash(Filler(5)), EncodeEoa(balance: 5000, nonce: 3));
            account.SaveDirtyNodesToStorage();
            var rootAsOfN = account.Root.GetHash();

            shim.Block = 3;
            account.Put(AddrHash(TargetAddr), EncodeEoa(BalanceAtHead, nonce: 2));
            account.SaveDirtyNodesToStorage();
            var rootHead = account.Root.GetHash();

            pathStore.Flush();
            return (pathStore, journal, root1, rootAsOfN, rootHead);
        }

        [Fact]
        public void FindBlockByStateRoot_Writes_Seeks_And_Prunes()
        {
            var (_, journal, root1, rootAsOfN, rootHead) = JournalThreeBlocks();

            Assert.Equal(1UL, journal.FindBlockByStateRoot(root1));
            Assert.Equal(2UL, journal.FindBlockByStateRoot(rootAsOfN));
            Assert.Equal(3UL, journal.FindBlockByStateRoot(rootHead));

            var unknown = new byte[32];
            for (int i = 0; i < 32; i++) unknown[i] = 0x7c;
            Assert.Null(journal.FindBlockByStateRoot(unknown));

            var path = new RocksDbPathTrieNodeStore(_mgr);
            journal.PruneBelow(3);
            Assert.Null(journal.FindBlockByStateRoot(rootAsOfN));
            Assert.Null(journal.FindBlockByStateRoot(root1));
            Assert.Equal(3UL, journal.FindBlockByStateRoot(rootHead));
        }

        private sealed class FakeBlockStore : IBlockStore
        {
            private readonly BlockHeader _latest;
            public FakeBlockStore(BlockHeader latest) { _latest = latest; }
            public Task<BlockHeader> GetLatestAsync() => Task.FromResult(_latest);
            public Task<BigInteger> GetHeightAsync() => Task.FromResult((BigInteger)(ulong)_latest.BlockNumber);
            public Task<BlockHeader> GetByHashAsync(byte[] hash) => Task.FromResult<BlockHeader>(null);
            public Task<BlockHeader> GetByNumberAsync(BigInteger number)
                => Task.FromResult(number == (BigInteger)(ulong)_latest.BlockNumber ? _latest : null);
            public Task SaveAsync(BlockHeader header, byte[] blockHash) => Task.CompletedTask;
            public Task<bool> ExistsAsync(byte[] hash) => Task.FromResult(false);
            public Task<byte[]> GetHashByNumberAsync(BigInteger number) => Task.FromResult<byte[]>(null);
            public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash) => Task.CompletedTask;
            public Task DeleteByNumberAsync(BigInteger blockNumber) => Task.CompletedTask;
        }

        [Fact]
        public async Task ResolveForRootAsync_Selects_AsOfN_Declines_Latest_Unknown_And_BelowFloor()
        {
            var (pathStore, journal, root1, rootAsOfN, rootHead) = JournalThreeBlocks();

            ulong head = 3;
            var headHeader = new BlockHeader { StateRoot = rootHead, BlockNumber = head };
            var blocks = new FakeBlockStore(headHeader);
            var state = new RocksDbStateStore(_mgr);
            var floor = new FixedWindowFloorPolicy(1);
            var metadata = new Nethereum.CoreChain.Storage.InMemory.InMemoryChainMetadataStore();
            metadata.Commit(head, new byte[32]);
            var serving = new HistoricalNodeServing(journal, pathStore, floor, indexOn: true, state, blocks, metadata);

            Assert.Null(await serving.ResolveForRootAsync(rootHead));

            var unknown = new byte[32];
            for (int i = 0; i < 32; i++) unknown[i] = 0x3a;
            Assert.Null(await serving.ResolveForRootAsync(unknown));

            Assert.Null(await serving.ResolveForRootAsync(root1));

            var store = await serving.ResolveForRootAsync(rootAsOfN);
            Assert.NotNull(store);
            var trie = PatriciaTrie.LoadFromStorage(rootAsOfN, store);
            var body = trie.Get(AddrHash(TargetAddr));
            Assert.NotNull(body);
            var acct = new AccountEncoder().Decode(body);
            Assert.Equal((ulong)BalanceAtN, acct.Balance.ToULong());
        }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
