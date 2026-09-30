using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.RocksDB.UnitTests.Sync;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PathKeyedLatestProofServeTests : IDisposable
    {
        private const string TargetAddr = "0x0000000000000000000000000000000000000001";

        private readonly string _root;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();

        public PathKeyedLatestProofServeTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "necc-latest-serve-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
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

        private static async Task SaveHeaderAsync(IBlockStore blocks, ulong number, byte[] stateRoot)
        {
            var hash = _keccak.CalculateHash(BitConverter.GetBytes(number).Concat(stateRoot).ToArray());
            var header = new BlockHeader
            {
                ParentHash = new byte[32],
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
            var context = new RpcContext(node, BigInteger.Parse("3503995874084926"), services);
            var request = new RpcRequestMessage(1, "eth_getProof", TargetAddr, new string[0], blockParam);
            return await handler.HandleAsync(request, context);
        }

        private static byte[] BuildLatestState(IChainStoreBundle bundle)
        {
            var store = (ITrieNodeStore)bundle.StateTrieNodes;
            var account = new PatriciaTrie(store, _hp) { Tracer = new TrieTracer() };
            account.Put(AddrHash(TargetAddr), EncodeEoa(balance: 100, nonce: 1));
            for (int i = 2; i <= 10; i++) account.Put(AddrHash(Filler(i)), EncodeEoa(balance: i, nonce: 0));
            account.SaveDirtyNodesToStorage();
            var root = account.Root.GetHash();
            store.Flush();
            return root;
        }

        private RocksDbChainStoreBundle OpenPathKeyed(string name)
            => RocksDbChainStoreBundle.Open(Path.Combine(_root, name), storageOptions: new RocksDbStorageOptions
            {
                PathKeyedState = true,
            });

        [Fact]
        public async Task PathKeyed_Node_Serves_Latest_GetProof_From_PathStore()
        {
            using var bundle = OpenPathKeyed("node");
            var root = BuildLatestState(bundle);
            await SaveHeaderAsync(bundle.Blocks, 1, root);

            var node = BuildNode(bundle);

            var resp = await GetProofAsync(node, "latest");
            Assert.Null(resp.Error);
            var proof = ToAccountProof(resp.Result);
            Assert.Equal((BigInteger)100, proof.Balance.Value);
            Assert.Equal((BigInteger)1, proof.Nonce.Value);
            Assert.True(Verify(proof, root), "latest proof must verify against the latest state root");
        }

        [Fact]
        public async Task PathKeyed_Snap_Handler_From_LatestStore_Answers_AccountRange()
        {
            using var bundle = OpenPathKeyed("snap");
            var root = BuildLatestState(bundle);

            var latest = ((ILatestProofServingBundle)bundle).LatestProofNodeStore;
            Assert.NotNull(latest);

            var handler = new PatriciaSnapRequestHandler(latest, new StateStoreBytecodeStore(bundle.State));
            var resp = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 1,
                RootHash = root,
                StartingHash = new byte[32],
                LimitHash = FilledHash(0xff),
                ResponseBytes = 1_000_000UL,
            });

            Assert.Equal(10, resp.Accounts.Count);
            Assert.NotEmpty(resp.Proof);
            for (int i = 1; i < resp.Accounts.Count; i++)
                Assert.True(ByteArrayComparer.Current.Compare(resp.Accounts[i - 1].Hash, resp.Accounts[i].Hash) < 0);

            var targetHash = AddrHash(TargetAddr);
            Assert.Contains(resp.Accounts, a => a.Hash.SequenceEqual(targetHash));
        }

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        private static AccountProof ToAccountProof(object result)
        {
            if (result is AccountProof ap) return ap;
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<AccountProof>(json);
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
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
