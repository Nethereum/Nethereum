using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Server.Bootstrap;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    /// <summary>
    /// End-to-end snap-bootstrap: build a small synthetic state on one
    /// "server side" in an InMemoryTrieStorage + bytecode dict, serve it via
    /// <see cref="PatriciaSnapRequestHandler"/> + <see cref="InProcessSnapPeer"/>,
    /// run <see cref="SnapBootstrapper.RunAsync"/> against a fresh RocksDB
    /// bundle on the "client side", then verify the client's
    /// <see cref="TrieFallbackStateStore"/> resolves the original accounts +
    /// storage byte-for-byte.
    /// </summary>
    public class SnapBootstrapperE2ETests : IDisposable
    {
        private readonly string _dbPath;

        private const string AddrA = "0x0000000000000000000000000000000000000001";
        private const string AddrB = "0x0000000000000000000000000000000000000002";

        public SnapBootstrapperE2ETests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_snapboot_e2e_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, true); } catch { }
            }
        }

        [Fact]
        public async Task SnapBootstrap_FromInProcessServer_StateRootMatches_AndAccountsResolveViaFallback()
        {
            // --- SERVER SIDE: build small state, host snap/1 in-process
            var serverTrie = new InMemoryTrieStorage();
            var stateTrie = new PatriciaTrie();
            var bytecodes = new Dictionary<string, byte[]>();

            var accAHash = HashAddress(AddrA);
            var accBHash = HashAddress(AddrB);

            // AddrA — EOA, balance + nonce
            var accA = new Account { Nonce = 1, Balance = 1_000_000UL,
                CodeHash = DefaultValues.EMPTY_DATA_HASH, StateRoot = DefaultValues.EMPTY_TRIE_HASH };
            stateTrie.Put(accAHash, new AccountEncoder().Encode(accA), serverTrie);

            // AddrB — contract with code + 2 storage slots
            var code = new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            bytecodes[codeHash.ToHex()] = code;

            var storageTrieB = new PatriciaTrie();
            var slot0Key = StateKeys.StorageSlotKey(BigInteger.Zero);
            var slot1Key = StateKeys.StorageSlotKey(BigInteger.One);
            var slot0Value = RLP.RLP.EncodeElement(new byte[] { 0xAA });
            var slot1Value = RLP.RLP.EncodeElement(new byte[] { 0xBB, 0xCC });
            storageTrieB.Put(slot0Key, slot0Value, serverTrie);
            storageTrieB.Put(slot1Key, slot1Value, serverTrie);
            storageTrieB.SaveDirtyNodesToStorage(serverTrie);
            var accBStorageRoot = storageTrieB.Root.GetHash();

            var accB = new Account { Nonce = 7, Balance = 5_000UL,
                CodeHash = codeHash, StateRoot = accBStorageRoot };
            stateTrie.Put(accBHash, new AccountEncoder().Encode(accB), serverTrie);

            stateTrie.SaveDirtyNodesToStorage(serverTrie);
            var pivotStateRoot = stateTrie.Root.GetHash();

            var bytecodeStore = new InlineBytecodeStore(bytecodes);
            var handler = new PatriciaSnapRequestHandler(serverTrie, bytecodeStore);
            var peer = new InProcessSnapPeer(handler);

            // --- CLIENT SIDE: empty RocksDB bundle, run snap-bootstrap
            using var bundle = RocksDbChainStoreBundle.Open(_dbPath);
            Assert.Equal(0UL, bundle.Metadata.GetLastBlock());

            var pivotHeader = new BlockHeader
            {
                BlockNumber = 1_000_000,
                StateRoot = pivotStateRoot,
                ParentHash = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                ExtraData = Array.Empty<byte>(),
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = 0,
                GasLimit = 0,
                GasUsed = 0,
                Timestamp = 0,
                MixHash = new byte[32],
                Nonce = new byte[8]
            };
            var pivotHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0xCA, 0xFE });

            var result = await SnapBootstrapper.RunAsync(
                bundle, peer, pivotHeader, pivotHash,
                NullLogger.Instance);

            Assert.True(result.Ran);
            Assert.True(result.AccountCount >= 2);
            Assert.True(result.SlotCount >= 2);
            Assert.True(result.BytecodeCount >= 1);
            Assert.Equal(1_000_000UL, bundle.Metadata.GetLastBlock());

            // --- READ PATH: TrieFallbackStateStore over the client bundle resolves
            //                accounts + storage that snap delivered.
            var fallback = new TrieFallbackStateStore(
                bundle.State,
                (ITrieStorage)bundle.TrieNodes,
                () => pivotStateRoot);

            var fetchedA = await fallback.GetAccountAsync(AddrA);
            Assert.NotNull(fetchedA);
            Assert.Equal((BigInteger)1, (BigInteger)fetchedA.Nonce);
            Assert.Equal((BigInteger)1_000_000UL, (BigInteger)fetchedA.Balance);

            var fetchedB = await fallback.GetAccountAsync(AddrB);
            Assert.NotNull(fetchedB);
            Assert.Equal((BigInteger)7, (BigInteger)fetchedB.Nonce);
            Assert.Equal((BigInteger)5_000UL, (BigInteger)fetchedB.Balance);
            Assert.Equal(codeHash, fetchedB.CodeHash);

            var slot0 = await fallback.GetStorageAsync(AddrB, BigInteger.Zero);
            Assert.Equal(new byte[] { 0xAA }, slot0);

            var slot1 = await fallback.GetStorageAsync(AddrB, BigInteger.One);
            Assert.Equal(new byte[] { 0xBB, 0xCC }, slot1);

            // --- BYTECODE: persisted via bundle.State.SaveCodeAsync inside the sink
            var fetchedCode = await bundle.State.GetCodeAsync(codeHash);
            Assert.Equal(code, fetchedCode);
        }

        [Fact]
        public async Task SnapBootstrap_BundleAlreadyHasState_SkipsAndReturnsFalse()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dbPath);
            bundle.Metadata.Commit(500, Sha3Keccack.Current.CalculateHash(new byte[] { 0x01 }));

            var fakeHeader = new BlockHeader
            {
                BlockNumber = 1,
                StateRoot = new byte[32],
                ParentHash = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                ExtraData = Array.Empty<byte>(),
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                MixHash = new byte[32],
                Nonce = new byte[8]
            };
            var fakeHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0xDE, 0xAD });

            var result = await SnapBootstrapper.RunAsync(
                bundle, new InProcessSnapPeer(new ThrowingHandler()),
                fakeHeader, fakeHash, NullLogger.Instance);

            Assert.False(result.Ran);
            Assert.Contains("existing state", result.SkipReason);
        }

        private static byte[] HashAddress(string address)
            => Sha3Keccack.Current.CalculateHash(
                AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray());

        private sealed class InlineBytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<string, byte[]> _byHash;
            public InlineBytecodeStore(Dictionary<string, byte[]> byHash) { _byHash = byHash; }
            public byte[] Get(byte[] codeHash)
                => _byHash.TryGetValue(codeHash.ToHex(), out var c) ? c : null;
        }

        private sealed class ThrowingHandler : ISnapRequestHandler
        {
            public Task<Model.P2P.Snap.AccountRangeMessage> GetAccountRangeAsync(
                Model.P2P.Snap.GetAccountRangeMessage req, System.Threading.CancellationToken ct = default)
                => throw new InvalidOperationException("snap path should not be entered when bundle already has state");
            public Task<Model.P2P.Snap.StorageRangesMessage> GetStorageRangesAsync(
                Model.P2P.Snap.GetStorageRangesMessage req, System.Threading.CancellationToken ct = default)
                => throw new InvalidOperationException();
            public Task<Model.P2P.Snap.ByteCodesMessage> GetByteCodesAsync(
                Model.P2P.Snap.GetByteCodesMessage req, System.Threading.CancellationToken ct = default)
                => throw new InvalidOperationException();
            public Task<Model.P2P.Snap.TrieNodesMessage> GetTrieNodesAsync(
                Model.P2P.Snap.GetTrieNodesMessage req, System.Threading.CancellationToken ct = default)
                => throw new InvalidOperationException();
        }
    }
}
