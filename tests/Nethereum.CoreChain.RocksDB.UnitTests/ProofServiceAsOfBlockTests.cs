using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Documentation;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class ProofServiceAsOfBlockTests : IDisposable
    {
        private const string TargetAddr = "0x0000000000000000000000000000000000000001";
        private static readonly BigInteger BalanceAtN = 100;
        private static readonly BigInteger BalanceAtHead = 999;

        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();

        public ProofServiceAsOfBlockTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-asof-proof-" + Guid.NewGuid().ToString("N"));
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

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "state-serving", "As-of-block vs latest proofs report consistent account values")]
        public async Task Proof_Reports_AsOfN_Vs_Latest_Account_Values_Both_Consistent()
        {
            var pathStore = new RocksDbPathTrieNodeStore(_mgr);
            var journal = new RocksDbNodeReverseDiffStore(_mgr, buildKeyMajorIndex: true);
            var shim = new JournalingStore(pathStore, journal);
            var account = new PatriciaTrie(shim, _hp) { Tracer = new TrieTracer() };

            shim.Block = 1;
            account.Put(AddrHash(TargetAddr), EncodeEoa(BalanceAtN, nonce: 1));
            for (int i = 2; i <= 10; i++) account.Put(AddrHash(Filler(i)), EncodeEoa(balance: i, nonce: 0));
            account.SaveDirtyNodesToStorage();

            shim.Block = 2;
            account.Put(AddrHash(Filler(5)), EncodeEoa(balance: 5000, nonce: 3));
            account.SaveDirtyNodesToStorage();
            var rootAsOfN = account.Root.GetHash();

            shim.Block = 3;
            account.Put(AddrHash(TargetAddr), EncodeEoa(BalanceAtHead, nonce: 2));
            account.SaveDirtyNodesToStorage();
            var rootHead = account.Root.GetHash();

            pathStore.Flush();

            Assert.False(rootAsOfN.SequenceEqual(rootHead), "roots must differ across the target's change");

            var stateStore = new RocksDbStateStore(_mgr);

            var asOfN = new AsOfBlockNodeStore(journal, pathStore, 2);
            var svcN = new ProofService(stateStore, asOfN);
            var proofN = await svcN.GenerateAccountProofAsync(TargetAddr, new List<BigInteger>(), rootAsOfN);

            var asOfHead = new AsOfBlockNodeStore(journal, pathStore, 3);
            var svcHead = new ProofService(stateStore, asOfHead);
            var proofHead = await svcHead.GenerateAccountProofAsync(TargetAddr, new List<BigInteger>(), rootHead);

            Assert.Equal(BalanceAtN, proofN.Balance.Value);
            Assert.Equal((BigInteger)1, proofN.Nonce.Value);
            Assert.Equal(BalanceAtHead, proofHead.Balance.Value);
            Assert.Equal((BigInteger)2, proofHead.Nonce.Value);

            Assert.True(Verify(proofN, rootAsOfN), "as-of-N proof must verify against the as-of-N root");
            Assert.True(Verify(proofHead, rootHead), "latest proof must verify against the head root");

            Assert.False(Verify(proofN, rootHead), "as-of-N account must not verify against the head root");
        }

        // WS4 Bug B1 — a historical (as-of-N) account proof for an address whose as-of-N LEAF has an EMPTY
        // storageRoot must NOT touch the flat LATEST storage store: the pre-fix code fell into the flat fallback,
        // called RootCalculator.CalculateStorageRoot -> trie.SaveNodesToStorage -> Commit on the READ-ONLY
        // AsOfBlockNodeStore and threw NotSupportedException. The EIP-4788 beacon contract is exactly this shape
        // pre-Cancun (empty storage as-of-N, populated storage at latest). Repro: the target's as-of-N leaf has an
        // empty storageRoot, yet non-empty storage exists for it in the flat store.
        [Fact]
        public async Task Proof_AsOfN_EmptyLeafStorageRoot_DoesNotReadLatestFlatStorage()
        {
            var pathStore = new RocksDbPathTrieNodeStore(_mgr);
            var journal = new RocksDbNodeReverseDiffStore(_mgr, buildKeyMajorIndex: true);
            var shim = new JournalingStore(pathStore, journal);
            var account = new PatriciaTrie(shim, _hp) { Tracer = new TrieTracer() };

            shim.Block = 1;
            account.Put(AddrHash(TargetAddr), EncodeEoa(BalanceAtN, nonce: 1));
            for (int i = 2; i <= 10; i++) account.Put(AddrHash(Filler(i)), EncodeEoa(balance: i, nonce: 0));
            account.SaveDirtyNodesToStorage();

            shim.Block = 2;
            account.Put(AddrHash(Filler(5)), EncodeEoa(balance: 5000, nonce: 3));
            account.SaveDirtyNodesToStorage();
            var rootAsOfN = account.Root.GetHash();

            pathStore.Flush();

            var stateStore = new RocksDbStateStore(_mgr);
            await stateStore.SaveStorageAsync(TargetAddr, slot: 1, value: new byte[] { 0x42 });
            var latestStorage = await stateStore.GetAllStorageAsync(TargetAddr);
            Assert.True(latestStorage.Count > 0, "flat latest storage must be non-empty to exercise the fallback");

            var asOfN = new AsOfBlockNodeStore(journal, pathStore, 2);
            var svc = new ProofService(stateStore, asOfN);

            var proof = await svc.GenerateAccountProofAsync(TargetAddr, new List<BigInteger>(), rootAsOfN);

            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH.ToHex(true), proof.StorageHash);

            Assert.True(Verify(proof, rootAsOfN), "as-of-N account proof must verify against the as-of-N root");
        }

        [Fact]
        public async Task Proof_AsOfN_EmptyLeafStorage_StorageProofsAreZero_NotLatest()
        {
            var pathStore = new RocksDbPathTrieNodeStore(_mgr);
            var journal = new RocksDbNodeReverseDiffStore(_mgr, buildKeyMajorIndex: true);
            var shim = new JournalingStore(pathStore, journal);
            var account = new PatriciaTrie(shim, _hp) { Tracer = new TrieTracer() };

            shim.Block = 1;
            account.Put(AddrHash(TargetAddr), EncodeEoa(BalanceAtN, nonce: 1));
            for (int i = 2; i <= 10; i++) account.Put(AddrHash(Filler(i)), EncodeEoa(balance: i, nonce: 0));
            account.SaveDirtyNodesToStorage();

            shim.Block = 2;
            account.Put(AddrHash(Filler(5)), EncodeEoa(balance: 5000, nonce: 3));
            account.SaveDirtyNodesToStorage();
            var rootAsOfN = account.Root.GetHash();
            pathStore.Flush();

            var stateStore = new RocksDbStateStore(_mgr);
            await stateStore.SaveStorageAsync(TargetAddr, slot: 1, value: new byte[] { 0x42 });

            var asOfN = new AsOfBlockNodeStore(journal, pathStore, 2);
            var svc = new ProofService(stateStore, asOfN);

            var proof = await svc.GenerateAccountProofAsync(TargetAddr, new List<BigInteger> { 1 }, rootAsOfN);

            Assert.Single(proof.StorageProof);
            Assert.Equal(BigInteger.Zero, proof.StorageProof[0].Value.Value);
            Assert.Empty(proof.StorageProof[0].Proof);
            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH.ToHex(true), proof.StorageHash);
        }

        private static bool Verify(Nethereum.RPC.Eth.DTOs.AccountProof proof, byte[] stateRoot)
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
