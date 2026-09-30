using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
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
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Snap
{
    public class TrieFallbackStateStoreTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;
        private readonly RocksDbStateStore _inner;
        private readonly RocksDbTrieNodeStore _trieNodes;

        private const string AddrA = "0x0000000000000000000000000000000000000001";
        private const string AddrB = "0x0000000000000000000000000000000000000002";
        private const string AddrC = "0x0000000000000000000000000000000000000003";

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

            var slot0 = await fallback.GetStorageAsync(AddrB, BigInteger.Zero);
            var slot1 = await fallback.GetStorageAsync(AddrB, BigInteger.One);

            Assert.NotNull(slot0);
            Assert.Equal(new byte[] { 0xAA }, slot0);
            Assert.NotNull(slot1);
            Assert.Equal(new byte[] { 0xBB, 0xCC }, slot1);
        }

        [Fact]
        public async Task GetAllStorageAsync_SharedStorageRoot_EmptyFlat_ResolvesViaTrie()
        {
            var finalRoot = await StreamStateWithSharedStorageContract();
            var fallback = new TrieFallbackStateStore(_inner, _trieNodes, () => finalRoot);

            var storage = await fallback.GetAllStorageAsync(AddrC);

            Assert.Equal(2, storage.Count);
            Assert.Equal(new byte[] { 0xAA }, storage[StateKeys.StorageSlotKey(BigInteger.Zero)]);
            Assert.Equal(new byte[] { 0xBB, 0xCC }, storage[StateKeys.StorageSlotKey(BigInteger.One)]);
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
            var sink = new TrieSnapSyncSink(_trieNodes, _inner);
            await sink.BeginAsync(new byte[32], default);

            await WriteAccount(sink, AddrA,
                nonce: 1, balance: 1_000_000UL,
                codeHash: DefaultValues.EMPTY_DATA_HASH,
                storageRoot: DefaultValues.EMPTY_TRIE_HASH);

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

        private async Task<byte[]> StreamStateWithSharedStorageContract()
        {
            var sink = new TrieSnapSyncSink(_trieNodes, _inner);
            await sink.BeginAsync(new byte[32], default);

            var code = new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);

            var storageRoot = await StreamAccountStorage(sink, AddrB);
            await WriteAccount(sink, AddrB, nonce: 7, balance: 5_000UL, codeHash: codeHash, storageRoot: storageRoot);
            await sink.WriteBytecodeAsync(codeHash, code, default);

            await WriteAccount(sink, AddrC, nonce: 7, balance: 9_000UL, codeHash: codeHash, storageRoot: storageRoot);

            return await sink.FinaliseRootAsync(default);
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

            var scope = await sink.BeginAccountStorageAsync(addrHash, expectedRoot, default);
            await scope.WriteSlotAsync(slot0Key, slot0Value, default);
            await scope.WriteSlotAsync(slot1Key, slot1Value, default);
            await scope.EndAsync(default);

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
