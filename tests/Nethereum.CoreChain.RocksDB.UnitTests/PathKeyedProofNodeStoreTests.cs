using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PathKeyedProofNodeStoreTests : IDisposable
    {
        private const string TargetAddr = "0x0000000000000000000000000000000000000001";

        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();

        public PathKeyedProofNodeStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-pathproof-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
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

        private (RocksDbPathTrieNodeStore path, byte[] root) BuildLatestState()
        {
            var pathStore = new RocksDbPathTrieNodeStore(_mgr);
            var account = new PatriciaTrie(pathStore, _hp) { Tracer = new TrieTracer() };
            account.Put(AddrHash(TargetAddr), EncodeEoa(balance: 100, nonce: 1));
            for (int i = 2; i <= 10; i++) account.Put(AddrHash(Filler(i)), EncodeEoa(balance: i, nonce: 0));
            account.SaveDirtyNodesToStorage();
            var root = account.Root.GetHash();
            pathStore.Flush();
            return (pathStore, root);
        }

        [Fact]
        public void ContainsKey_True_For_LatestRoot_False_For_Bogus()
        {
            var (path, root) = BuildLatestState();
            var adapter = new PathKeyedProofNodeStore(path);

            Assert.True(adapter.ContainsKey(root), "the latest account-trie root is served from the path store");

            var bogus = _keccak.CalculateHash(new byte[] { 9, 9, 9 });
            Assert.False(adapter.ContainsKey(bogus), "a root the path store does not hold must fail the gate");
            Assert.False(adapter.ContainsKey(null));
            Assert.False(adapter.ContainsKey(new byte[10]));
        }

        [Fact]
        public void Get_And_Contains_Node_Resolve_From_Path_Store()
        {
            var (path, root) = BuildLatestState();
            var adapter = new PathKeyedProofNodeStore(path);

            var rootRef = PatriciaTrie.LoadFromStorage(root, adapter).Root;
            Assert.True(adapter.Contains(rootRef));
            var blob = adapter.Get(rootRef);
            Assert.NotNull(blob);
            Assert.True(_hp.ComputeHash(blob).SequenceEqual(root), "verify-on-read: blob keccak == root reference");
        }

        [Fact]
        public void Write_And_HashKeyed_Members_Are_NotSupported()
        {
            var (path, _) = BuildLatestState();
            var adapter = new PathKeyedProofNodeStore(path);

            Assert.Throws<NotSupportedException>(() => adapter.Get(new byte[32]));
            Assert.Throws<NotSupportedException>(() => adapter.Put(new byte[1], new byte[1]));
            Assert.Throws<NotSupportedException>(() => adapter.Delete(new byte[1]));
            Assert.Throws<NotSupportedException>(() => adapter.Commit(new TrieNodeSet()));
            Assert.Throws<NotSupportedException>(() => adapter.Clear());
        }

        [Fact]
        public async System.Threading.Tasks.Task Full_Latest_Account_Proof_Over_Adapter_Verifies_Against_Root()
        {
            var (path, root) = BuildLatestState();
            var adapter = new PathKeyedProofNodeStore(path);
            var stateStore = new RocksDbStateStore(_mgr);

            var svc = new ProofService(stateStore, adapter);
            var proof = await svc.GenerateAccountProofAsync(TargetAddr, new List<BigInteger>(), root);

            Assert.Equal((BigInteger)100, proof.Balance.Value);
            Assert.Equal((BigInteger)1, proof.Nonce.Value);
            Assert.True(Verify(proof, root), "latest proof over the path-store adapter must verify against the latest root");
        }

        [Fact]
        public void Bundle_Exposes_LatestProofNodeStore_Only_When_PathKeyed()
        {
            var root = Path.Combine(_dir, "bundles");
            Directory.CreateDirectory(root);

            using (var hash = RocksDbChainStoreBundle.Open(Path.Combine(root, "hash"),
                       storageOptions: new RocksDbStorageOptions()))
            {
                Assert.Null(((ILatestProofServingBundle)hash).LatestProofNodeStore);
            }

            using (var pathKeyed = RocksDbChainStoreBundle.Open(Path.Combine(root, "path"),
                       storageOptions: new RocksDbStorageOptions { PathKeyedState = true }))
            {
                var latest = ((ILatestProofServingBundle)pathKeyed).LatestProofNodeStore;
                Assert.NotNull(latest);
                Assert.IsType<PathKeyedProofNodeStore>(latest);
            }

            using (var pathHist = RocksDbChainStoreBundle.Open(Path.Combine(root, "path-hist"),
                       storageOptions: new RocksDbStorageOptions
                       {
                           PathKeyedState = true,
                           TrieNodeHistoryBlocks = 128,
                           TrieNodeHistoryIndex = true,
                       }))
            {
                Assert.NotNull(((ILatestProofServingBundle)pathHist).LatestProofNodeStore);
            }
        }

        private static bool Verify(AccountProof proof, byte[] stateRoot)
        {
            var proofBytes = proof.AccountProofs.Select(p => p.HexToByteArray()).ToList();
            var account = new Account
            {
                Balance = proof.Balance.Value,
                Nonce = proof.Nonce.Value,
                CodeHash = proof.CodeHash.HexToByteArray(),
                StateRoot = proof.StorageHash.HexToByteArray()
            };
            return ProofVerification.Current.Account.Verify(stateRoot, proofBytes, TargetAddr, account);
        }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
