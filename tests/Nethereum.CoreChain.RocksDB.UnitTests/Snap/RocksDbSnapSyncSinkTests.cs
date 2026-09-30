using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
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
    public class TrieSnapSyncSinkTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;
        private readonly RocksDbStateStore _state;
        private readonly RocksDbTrieNodeStore _trieNodes;

        public TrieSnapSyncSinkTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_snap_sink_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
            _state = new RocksDbStateStore(_manager);
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
        public async Task RocksDbAndInMemorySinks_OnSameSnapStream_ProduceIdenticalStateRoot()
        {
            var inMem = new InMemorySnapSyncSink();
            var rocks = new TrieSnapSyncSink(_trieNodes, _state);

            await StreamSyntheticState(inMem);
            await StreamSyntheticState(rocks);

            var inMemRoot = await inMem.FinaliseRootAsync(default);
            var rocksRoot = await rocks.FinaliseRootAsync(default);

            Assert.Equal(inMemRoot.ToHex(), rocksRoot.ToHex());
            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH.ToHex(), rocksRoot.ToHex());
            Assert.Equal(3, rocks.AccountCount);
            Assert.Equal(2, rocks.SlotCount);
            Assert.Equal(1, rocks.BytecodeCount);
        }

        [Fact]
        public async Task RocksDbSink_PersistsBytecode_ReadableViaStateStore()
        {
            var sink = new TrieSnapSyncSink(_trieNodes, _state);
            var code = new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);

            await sink.WriteBytecodeAsync(codeHash, code, default);

            var read = await _state.GetCodeAsync(codeHash);
            Assert.Equal(code, read);
        }

        [Fact]
        public async Task RocksDbSink_StorageRootMismatch_ThrowsBeforeAffectingFinalRoot()
        {
            var sink = new TrieSnapSyncSink(_trieNodes, _state);
            var addrHash = Sha3Keccack.Current.CalculateHash(new byte[20]);
            var slotHash = Sha3Keccack.Current.CalculateHash(PadTo32(new byte[] { 0x01 }));

            var scope = await sink.BeginAccountStorageAsync(addrHash, expectedStorageRoot: new byte[32], default);
            await scope.WriteSlotAsync(
                slotHash,
                RLP.RLP.EncodeElement(new byte[] { 0x42 }),
                default);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => scope.EndAsync(default).AsTask());
            Assert.Null(_manager.Get(RocksDbManager.CF_STATE_STORAGE, FlatStorageKey(addrHash, slotHash)));
        }

        [Fact]
        public async Task RocksDbSink_StorageAbort_DoesNotPersistFlatStorage()
        {
            var sink = new TrieSnapSyncSink(_trieNodes, _state);
            var addrHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0xAB });
            var slotHash = Sha3Keccack.Current.CalculateHash(PadTo32(new byte[] { 0x02 }));

            var scope = await sink.BeginAccountStorageAsync(addrHash, expectedStorageRoot: new byte[32], default);
            await scope.WriteSlotAsync(
                slotHash,
                RLP.RLP.EncodeElement(new byte[] { 0x99 }),
                default);
            await scope.AbortAsync(default);

            Assert.Null(_manager.Get(RocksDbManager.CF_STATE_STORAGE, FlatStorageKey(addrHash, slotHash)));
        }

        private async Task StreamSyntheticState(ISnapSyncSink sink)
        {
            await sink.BeginAsync(targetRoot: new byte[32], default);

            await WriteAccount(sink,
                addressHash: AddrHash(0x01),
                nonce: 1,
                balance: 1_000_000UL,
                codeHash: DefaultValues.EMPTY_DATA_HASH,
                storageRoot: DefaultValues.EMPTY_TRIE_HASH);

            var code = new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            var storageRootB = await ComputeAndStreamStorage(sink, AddrHash(0x02));
            await WriteAccount(sink,
                addressHash: AddrHash(0x02),
                nonce: 7,
                balance: 5_000UL,
                codeHash: codeHash,
                storageRoot: storageRootB);
            await sink.WriteBytecodeAsync(codeHash, code, default);

            await WriteAccount(sink,
                addressHash: AddrHash(0x03),
                nonce: 0,
                balance: 999_999_999UL,
                codeHash: DefaultValues.EMPTY_DATA_HASH,
                storageRoot: DefaultValues.EMPTY_TRIE_HASH);
        }

        private async Task<byte[]> ComputeAndStreamStorage(ISnapSyncSink sink, byte[] addressHash)
        {
            var refTrie = new Merkle.Patricia.PatriciaTrie();
            var slotHash1 = Sha3Keccack.Current.CalculateHash(PadTo32(new byte[] { 0x00 }));
            var slotHash2 = Sha3Keccack.Current.CalculateHash(PadTo32(new byte[] { 0x01 }));
            var value1 = RLP.RLP.EncodeElement(new byte[] { 0xAA });
            var value2 = RLP.RLP.EncodeElement(new byte[] { 0xBB, 0xCC });
            refTrie.Put(slotHash1, value1);
            refTrie.Put(slotHash2, value2);
            var expectedRoot = refTrie.Root.GetHash();

            var scope = await sink.BeginAccountStorageAsync(addressHash, expectedRoot, default);
            await scope.WriteSlotAsync(slotHash1, value1, default);
            await scope.WriteSlotAsync(slotHash2, value2, default);
            await scope.EndAsync(default);

            return expectedRoot;
        }

        private static async Task WriteAccount(
            ISnapSyncSink sink, byte[] addressHash,
            ulong nonce, ulong balance,
            byte[] codeHash, byte[] storageRoot)
        {
            var account = new Account
            {
                Nonce = nonce,
                Balance = balance,
                CodeHash = codeHash,
                StateRoot = storageRoot
            };
            var canonical = new AccountEncoder().Encode(account);
            var slim = SlimAccountEncoder.ToSlim(canonical);
            await sink.WriteAccountAsync(addressHash, slim, default);
        }

        private static byte[] AddrHash(byte seed)
        {
            var addr = new byte[20];
            addr[19] = seed;
            return Sha3Keccack.Current.CalculateHash(addr);
        }

        private static byte[] FlatStorageKey(byte[] accountHash, byte[] slotHash)
        {
            var key = new byte[64];
            Buffer.BlockCopy(accountHash, 0, key, 0, 32);
            Buffer.BlockCopy(slotHash, 0, key, 32, 32);
            return key;
        }
        private static byte[] PadTo32(byte[] src)
        {
            if (src.Length == 32) return src;
            var padded = new byte[32];
            Array.Copy(src, 0, padded, 32 - src.Length, src.Length);
            return padded;
        }
    }
}
