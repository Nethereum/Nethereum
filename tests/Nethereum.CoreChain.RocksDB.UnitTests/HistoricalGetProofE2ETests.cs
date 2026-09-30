using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.RocksDB.UnitTests.Sync;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Documentation;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class HistoricalGetProofE2ETests : IDisposable
    {
        private const string TargetAddr = "0x0000000000000000000000000000000000000001";

        private readonly string _root;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();

        public HistoricalGetProofE2ETests()
        {
            _root = Path.Combine(Path.GetTempPath(), "necc-hist-getproof-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
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

        private sealed class InertBlockSource : IBlockSource
        {
            public DivergenceSignal LastChainBreak => null;
            public async IAsyncEnumerable<BlockBundle> StreamAsync(ulong fromBlock,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            { await Task.CompletedTask; yield break; }
            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct) => Task.FromResult(BlockSourceHealth.Healthy);
            public Task ReportBadBundleAsync(ulong b, BadBundleReason r, CancellationToken ct) => Task.CompletedTask;
        }

        private sealed class FixedPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong b) => false;
            public ValidationAction OnVerdict(DivergenceVerdict v, ulong b) => ValidationAction.RewindAndRetry;
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

        private static FollowerChainNode BuildNode(IChainStoreBundle bundle)
        {
            var chainConfig = HiveTestdataFixture.ChainConfigFactory(HardforkName.Cancun);
            var hardforkConfig = HiveTestdataFixture.HardforkConfigFactory(HardforkName.Cancun);
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            var txProcessor = new TransactionProcessor(bundle.State, bundle.Blocks, chainConfig, txVerifier, hardforkConfig);
            return new FollowerChainNode(
                bundle: bundle,
                source: new InertBlockSource(),
                executorFactory: _ => throw new InvalidOperationException("executor not used in this read-only proof test"),
                policy: new FixedPolicy(),
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                chainConfig: chainConfig,
                hardforkConfig: hardforkConfig,
                txProcessor: txProcessor,
                txVerifier: txVerifier);
        }

        private static async Task SaveHeaderAsync(IBlockStore blocks, ulong number, byte[] stateRoot, byte[] parentHash)
        {
            var hash = _keccak.CalculateHash(BitConverter.GetBytes(number).Concat(stateRoot).ToArray());
            var header = new BlockHeader
            {
                ParentHash = parentHash ?? new byte[32],
                UnclesHash = new byte[32],
                Coinbase = AddressUtil.ZERO_ADDRESS,
                StateRoot = stateRoot,
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                LogsBloom = new byte[256],
                Difficulty = (EvmUInt256)0,
                BlockNumber = number,
                GasLimit = 0,
                GasUsed = 0,
                Timestamp = 0,
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
            };
            await blocks.SaveAsync(header, hash);
        }

        private static async Task<RpcResponseMessage> GetProofAsync(IChainNode node, string blockParam)
        {
            var handler = new EthGetProofHandler();
            var services = new ServiceCollection().BuildServiceProvider();
            var context = new RpcContext(node, MainnetGenesisConstants_ChainId, services);
            var request = new RpcRequestMessage(1, "eth_getProof", TargetAddr, new string[0], blockParam);
            return await handler.HandleAsync(request, context);
        }

        private static readonly BigInteger MainnetGenesisConstants_ChainId = BigInteger.Parse("3503995874084926");

        private (byte[] r1, byte[] r2, byte[] r3, byte[] r4) JournalFourBlocks(RocksDbChainStoreBundle bundle)
        {
            var journal = bundle.NodeServing.Journal;
            var pathStore = bundle.NodeServing.Latest;
            var shim = new JournalingStore(pathStore, journal);
            var account = new PatriciaTrie(shim, _hp) { Tracer = new TrieTracer() };

            shim.Block = 1;
            account.Put(AddrHash(TargetAddr), EncodeEoa(100, nonce: 1));
            for (int i = 2; i <= 10; i++) account.Put(AddrHash(Filler(i)), EncodeEoa(balance: i, nonce: 0));
            account.SaveDirtyNodesToStorage();
            var r1 = account.Root.GetHash();

            shim.Block = 2;
            account.Put(AddrHash(Filler(5)), EncodeEoa(balance: 5000, nonce: 3));
            account.SaveDirtyNodesToStorage();
            var r2 = account.Root.GetHash();

            shim.Block = 3;
            account.Put(AddrHash(TargetAddr), EncodeEoa(300, nonce: 2));
            account.SaveDirtyNodesToStorage();
            var r3 = account.Root.GetHash();

            shim.Block = 4;
            account.Put(AddrHash(TargetAddr), EncodeEoa(400, nonce: 3));
            account.SaveDirtyNodesToStorage();
            var r4 = account.Root.GetHash();

            pathStore.Flush();
            return (r1, r2, r3, r4);
        }

        private RocksDbChainStoreBundle OpenServing(string name)
            => RocksDbChainStoreBundle.Open(Path.Combine(_root, name), storageOptions: new RocksDbStorageOptions
            {
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 2,
                TrieNodeHistoryIndex = true,
            });

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

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "state-serving", "Serve an as-of-block eth_getProof within the retention window")]
        public async Task Node_Is_Capable_And_ProofServiceAsOf_Reports_AsOfN_Verifiable_Proof()
        {
            using var bundle = OpenServing("node");
            var (r1, r2, r3, r4) = JournalFourBlocks(bundle);
            await SaveHeaderAsync(bundle.Blocks, 1, r1, null);
            await SaveHeaderAsync(bundle.Blocks, 2, r2, null);
            await SaveHeaderAsync(bundle.Blocks, 3, r3, null);
            await SaveHeaderAsync(bundle.Blocks, 4, r4, null);

            var node = BuildNode(bundle);

            var capable = Assert.IsAssignableFrom<IHistoricalProofCapable>(node);
            Assert.True(capable.CanServeProofAsOf(4, head: 4), "head is always in-window");
            Assert.True(capable.CanServeProofAsOf(3, head: 4), "block 3 is within the window-2 floor (2)");
            Assert.True(capable.CanServeProofAsOf(2, head: 4), "the floor block itself is in-window");
            Assert.False(capable.CanServeProofAsOf(1, head: 4), "block 1 is below the floor");
            Assert.False(capable.CanServeProofAsOf(5, head: 4), "above head");

            var proof3 = await capable.ProofServiceAsOf(3).GenerateAccountProofAsync(TargetAddr, new List<BigInteger>(), r3);
            Assert.Equal((BigInteger)300, proof3.Balance.Value);
            Assert.True(Verify(proof3, r3), "as-of-3 proof must verify against block 3's root");
            Assert.False(Verify(proof3, r4), "as-of-3 account must not verify against the head root");

            var proof4 = await capable.ProofServiceAsOf(4).GenerateAccountProofAsync(TargetAddr, new List<BigInteger>(), r4);
            Assert.Equal((BigInteger)400, proof4.Balance.Value);
            Assert.True(Verify(proof4, r4));
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "state-serving", "eth_getProof declines below the retention floor with -32000")]
        public async Task Handler_Serves_InWindow_Historical_And_Fails_BelowFloor_And_Latest()
        {
            using var bundle = OpenServing("handler");
            var (r1, r2, r3, r4) = JournalFourBlocks(bundle);
            await SaveHeaderAsync(bundle.Blocks, 1, r1, null);
            await SaveHeaderAsync(bundle.Blocks, 2, r2, null);
            await SaveHeaderAsync(bundle.Blocks, 3, r3, null);
            await SaveHeaderAsync(bundle.Blocks, 4, r4, null);
            var node = BuildNode(bundle);

            var resp3 = await GetProofAsync(node, "0x3");
            Assert.Null(resp3.Error);
            var proof3 = ToAccountProof(resp3.Result);
            Assert.Equal((BigInteger)300, proof3.Balance.Value);
            Assert.True(Verify(proof3, r3));

            var resp1 = await GetProofAsync(node, "0x1");
            Assert.NotNull(resp1.Error);
            Assert.Equal(-32000, resp1.Error.Code);

            var respLatest = await GetProofAsync(node, "latest");
            Assert.Null(respLatest.Error);
            var proofLatest = ToAccountProof(respLatest.Result);
            Assert.Equal((BigInteger)400, proofLatest.Balance.Value);
            Assert.True(Verify(proofLatest, r4));
        }

        [Fact]
        public async Task HistoryOff_Node_Fails_Historical_With_32000()
        {
            using var bundle = RocksDbChainStoreBundle.Open(Path.Combine(_root, "histoff"),
                storageOptions: new RocksDbStorageOptions
                {
                    PathKeyedState = true,
                    TrieNodeHistoryBlocks = 2,
                    TrieNodeHistoryIndex = false,
                });
            Assert.Null(bundle.NodeServing);

            var randomRoot = _keccak.CalculateHash(new byte[] { 1, 2, 3 });
            await SaveHeaderAsync(bundle.Blocks, 3, randomRoot, null);
            await SaveHeaderAsync(bundle.Blocks, 4, _keccak.CalculateHash(new byte[] { 4 }), null);
            var node = BuildNode(bundle);

            Assert.False(((IHistoricalProofCapable)node).CanServeProofAsOf(3, head: 4));

            var resp = await GetProofAsync(node, "0x3");
            Assert.NotNull(resp.Error);
            Assert.Equal(-32000, resp.Error.Code);
        }

        private static AccountProof ToAccountProof(object result)
        {
            if (result is AccountProof ap) return ap;
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<AccountProof>(json);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
