using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Snap;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Snap
{
    /// <summary>
    /// End-to-end check of the snap-bootstrap read path:
    /// stream a small synthetic state through <see cref="RocksDbSnapSyncSink"/>
    /// (writes Patricia nodes + bytecode), confirm the flat
    /// <see cref="RocksDbStateStore"/> CFs are still empty (snap doesn't write
    /// flat-keyed account/storage rows), then verify
    /// <see cref="TrieFallbackStateStore"/> resolves accounts and storage via
    /// the trie and backfills the inner flat store so subsequent reads land
    /// fast-path.
    /// </summary>
    public class TrieFallbackStateStoreTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;
        private readonly RocksDbStateStore _inner;
        private readonly RocksDbTrieNodeStore _trieNodes;

        private const string AddrA = "0x0000000000000000000000000000000000000001";
        private const string AddrB = "0x0000000000000000000000000000000000000002";

        public TrieFallbackStateStoreTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_trieflbk_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
            _inner = new RocksDbStateStore(_manager);
            _trieNodes = new RocksDbTrieNodeStore(_manager);
        }

        public void Dispose()
        {
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, true); } catch { }
            }
        }

        [Fact]
        public async Task GetAccountAsync_AfterSnapStream_ResolvesViaTrieAndBackfills()
        {
            var (finalRoot, _) = await StreamSyntheticState();

            Assert.False(await _inner.AccountExistsAsync(AddrA),
                "Sanity: snap stream must not have populated the flat account CF directly.");

            var fallback = new TrieFallbackStateStore(_inner, _trieNodes, () => finalRoot);
            var account = await fallback.GetAccountAsync(AddrA);

            Assert.NotNull(account);
            Assert.Equal((BigInteger)1, (BigInteger)account.Nonce);
            Assert.Equal((BigInteger)1_000_000UL, (BigInteger)account.Balance);

            Assert.True(await _inner.AccountExistsAsync(AddrA),
                "Backfill must have populated the inner flat store after the first miss.");
        }

        [Fact]
        public async Task GetAccountAsync_Missing_ReturnsNull()
        {
            var (finalRoot, _) = await StreamSyntheticState();
            var fallback = new TrieFallbackStateStore(_inner, _trieNodes, () => finalRoot);

            var missing = await fallback.GetAccountAsync("0x000000000000000000000000000000000000ffff");

            Assert.Null(missing);
        }

        [Fact]
        public async Task GetStorageAsync_AfterSnapStream_ResolvesViaTrie()
        {
            var (finalRoot, _) = await StreamSyntheticState();
            var fallback = new TrieFallbackStateStore(_inner, _trieNodes, () => finalRoot);

            // Storage was written for AddrB at slot 0 -> 0xAA, slot 1 -> 0xBBCC
            var slot0 = await fallback.GetStorageAsync(AddrB, BigInteger.Zero);
            var slot1 = await fallback.GetStorageAsync(AddrB, BigInteger.One);

            Assert.NotNull(slot0);
            Assert.Equal(new byte[] { 0xAA }, slot0);
            Assert.NotNull(slot1);
            Assert.Equal(new byte[] { 0xBB, 0xCC }, slot1);
        }

        [Fact]
        public async Task GetAccountAsync_NoBackfill_LeavesInnerEmpty()
        {
            var (finalRoot, _) = await StreamSyntheticState();
            var fallback = new TrieFallbackStateStore(
                _inner, _trieNodes, () => finalRoot, backfill: false);

            var account = await fallback.GetAccountAsync(AddrA);

            Assert.NotNull(account);
            Assert.False(await _inner.AccountExistsAsync(AddrA),
                "backfill=false must leave the inner store untouched.");
        }

        [Fact]
        public async Task EmptyStateRoot_ReturnsNullWithoutWalkingTrie()
        {
            var fallback = new TrieFallbackStateStore(
                _inner, _trieNodes, () => DefaultValues.EMPTY_TRIE_HASH);

            var account = await fallback.GetAccountAsync(AddrA);
            Assert.Null(account);
        }

        private async Task<(byte[] finalRoot, byte[] storageRootB)> StreamSyntheticState()
        {
            var sink = new RocksDbSnapSyncSink(_trieNodes, _inner);
            await sink.BeginAsync(new byte[32], default);

            // AddrA — EOA, balance + nonce only
            await WriteAccount(sink, AddrA,
                nonce: 1, balance: 1_000_000UL,
                codeHash: DefaultValues.EMPTY_DATA_HASH,
                storageRoot: DefaultValues.EMPTY_TRIE_HASH);

            // AddrB — contract with 2 storage slots
            var code = new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            var storageRootB = await StreamAccountStorage(sink, AddrB);
            await WriteAccount(sink, AddrB,
                nonce: 7, balance: 5_000UL,
                codeHash: codeHash,
                storageRoot: storageRootB);
            await sink.WriteBytecodeAsync(codeHash, code, default);

            var finalRoot = await sink.FinaliseRootAsync(default);
            return (finalRoot, storageRootB);
        }

        private static async Task<byte[]> StreamAccountStorage(ISnapSyncSink sink, string address)
        {
            var addrHash = Sha3Keccack.Current.CalculateHash(
                AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray());

            var slot0Key = StateKeys.StorageSlotKey(BigInteger.Zero);
            var slot1Key = StateKeys.StorageSlotKey(BigInteger.One);
            var slot0Value = RLP.RLP.EncodeElement(new byte[] { 0xAA });
            var slot1Value = RLP.RLP.EncodeElement(new byte[] { 0xBB, 0xCC });

            var refTrie = new Merkle.Patricia.PatriciaTrie();
            refTrie.Put(slot0Key, slot0Value);
            refTrie.Put(slot1Key, slot1Value);
            var expectedRoot = refTrie.Root.GetHash();

            await sink.BeginAccountStorageAsync(addrHash, expectedRoot, default);
            await sink.WriteStorageSlotAsync(slot0Key, slot0Value, default);
            await sink.WriteStorageSlotAsync(slot1Key, slot1Value, default);
            await sink.EndAccountStorageAsync(default);

            return expectedRoot;
        }

        private static async Task WriteAccount(
            ISnapSyncSink sink, string address,
            ulong nonce, ulong balance,
            byte[] codeHash, byte[] storageRoot)
        {
            var addrHash = Sha3Keccack.Current.CalculateHash(
                AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray());

            var account = new Account
            {
                Nonce = nonce,
                Balance = balance,
                CodeHash = codeHash,
                StateRoot = storageRoot
            };
            var canonical = new AccountEncoder().Encode(account);
            var slim = SlimAccountEncoder.ToSlim(canonical);
            await sink.WriteAccountAsync(addrHash, slim, default);
        }
    }
}
