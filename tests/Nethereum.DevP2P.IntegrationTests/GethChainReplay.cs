using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.IntegrationTests.Helpers;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;
using Newtonsoft.Json.Linq;

namespace Nethereum.DevP2P.IntegrationTests
{
    public static class GethChainReplay
    {
        private static readonly BigInteger ChainId = BigInteger.Parse("3503995874084926");

        public sealed class Replayed : IDisposable
        {
            public RocksDbChainStoreBundle Bundle { get; init; }
            public FollowerChainNode Node { get; init; }
            public byte[][] Roots { get; init; }
            public ulong Head { get; init; }
            public ulong Floor { get; init; }
            public int BlockCount { get; init; }
            public int Matched { get; init; }
            public string FirstMismatch { get; init; }
            private string Dir { get; init; }

            public Replayed(string dir) { Dir = dir; }

            public async Task<RpcResponseMessage> GetProofAsync(string address, string[] storageKeys, string blockParam)
            {
                var handler = new EthGetProofHandler();
                var services = new ServiceCollection().BuildServiceProvider();
                var context = new RpcContext(Node, ChainId, services);
                var request = new RpcRequestMessage(1, "eth_getProof", address, storageKeys ?? new string[0], blockParam);
                return await handler.HandleAsync(request, context);
            }

            public void Dispose()
            {
                try { Node?.Dispose(); } catch { }
                try { Bundle?.Dispose(); } catch { }
                try { if (Dir != null && Directory.Exists(Dir)) Directory.Delete(Dir, recursive: true); } catch { }
            }
        }

        public static Nethereum.RPC.Eth.DTOs.AccountProof ToAccountProof(object result)
        {
            if (result is Nethereum.RPC.Eth.DTOs.AccountProof ap) return ap;
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Nethereum.RPC.Eth.DTOs.AccountProof>(json);
        }

        public static Replayed Replay(int pathKeyedWindow, bool historyIndex, int upToBlock)
            => ReplayAsync(pathKeyedWindow, historyIndex, upToBlock).GetAwaiter().GetResult();

        private static async Task<Replayed> ReplayAsync(int pathKeyedWindow, bool historyIndex, int upToBlock)
        {
            var testdata = GethToolLocator.FindEthTestTestdata();
            var dir = Path.Combine(Path.GetTempPath(), "necc-geth-pathkeyed-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            var bundle = RocksDbChainStoreBundle.Open(
                dir,
                journalOptions: HistoricalStateOptions.FullArchive,
                storageOptions: new RocksDbStorageOptions
                {
                    PathKeyedState = true,
                    TrieNodeHistoryBlocks = pathKeyedWindow,
                    TrieNodeHistoryIndex = historyIndex,
                });

            await LoadGenesisAsync(bundle.State, testdata);

            var calc = new IncrementalStateRootCalculator(
                bundle.State, bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));
            var engine = new BlockExecutor(
                bundle.State, bundle.Blocks, HiveTestdataChainActivations.Instance,
                chainConfigFactory: f => new ChainConfig
                {
                    ChainId = ChainId,
                    BaseFee = BigInteger.Zero,
                    Coinbase = AddressUtil.ZERO_ADDRESS,
                    Hardfork = f.ToString().ToLowerInvariant()
                },
                hardforkConfigFactory: f => DefaultMainnetHardforkRegistry.Instance.Get(f),
                stateRootCalculator: calc,
                rewardPolicy: EthereumProofOfWorkRewardPolicy.Instance,
                trieNodeStore: bundle.TrieNodes);
            var importer = new BlockImporter(
                engine, bundle.Blocks, bundle.State,
                transactionStore: bundle.Transactions,
                receiptStore: bundle.Receipts,
                logStore: bundle.Logs,
                uncleStore: bundle.Uncles,
                nodeCommitBlockContext: bundle.NodeCommitBlockSource,
                atomicFlush: bundle as IAtomicBlockFlush);

            var chainBytes = await File.ReadAllBytesAsync(Path.Combine(testdata, "chain.rlp"));

            var roots = new List<byte[]> { null };
            int pos = 0, blockNumber = 0, matched = 0;
            string firstMismatch = null;
            while (pos < chainBytes.Length)
            {
                var blockColl = (RLPCollection)RLP.RLP.DecodeFirstElement(chainBytes, pos);
                int consumed = RlpStreamHelpers.GetRlpItemLength(chainBytes, pos);
                pos += consumed;

                var header = new BlockHeaderEncoder().Decode(RlpStreamHelpers.ReEncodeAsList((RLPCollection)blockColl[0]));
                blockNumber++;

                var txList = (RLPCollection)blockColl[1];
                var transactions = new List<ISignedTransaction>();
                foreach (var txItem in txList)
                {
                    byte[] txBytes = txItem is RLPCollection coll ? RlpStreamHelpers.ReEncodeAsList(coll) : txItem.RLPData;
                    transactions.Add(TransactionFactory.CreateTransaction(txBytes));
                }

                var uncleList = (RLPCollection)blockColl[2];
                var uncles = new List<BlockHeader>();
                foreach (var u in uncleList)
                    uncles.Add(new BlockHeaderEncoder().Decode(RlpStreamHelpers.ReEncodeAsList((RLPCollection)u)));

                var withdrawals = new List<Withdrawal>();
                if (blockColl.Count >= 4 && blockColl[3] is RLPCollection wList)
                {
                    foreach (var wItem in wList)
                    {
                        var wColl = (RLPCollection)wItem;
                        var addr = wColl[2].RLPData;
                        var amount = wColl[3].RLPData == null || wColl[3].RLPData.Length == 0
                            ? 0UL
                            : (ulong)wColl[3].RLPData.ToBigIntegerFromRLPDecoded();
                        var wIndex = wColl[0].RLPData == null || wColl[0].RLPData.Length == 0
                            ? 0UL
                            : (ulong)wColl[0].RLPData.ToBigIntegerFromRLPDecoded();
                        var wValidatorIndex = wColl[1].RLPData == null || wColl[1].RLPData.Length == 0
                            ? 0UL
                            : (ulong)wColl[1].RLPData.ToBigIntegerFromRLPDecoded();
                        withdrawals.Add(new Withdrawal
                        {
                            Index = wIndex,
                            ValidatorIndex = wValidatorIndex,
                            Address = addr,
                            AmountInGwei = amount
                        });
                    }
                }

                var result = await importer.ImportAsync(header, transactions, uncles, withdrawals);
                roots.Add(header.StateRoot);
                if (result.RootMatches) matched++;
                else if (firstMismatch == null)
                    firstMismatch = $"block {blockNumber}: expected {header.StateRoot.ToHex()} got {result.ComputedStateRoot?.ToHex()} " +
                                    $"(fork={result.Fork}, txs={result.TransactionsExecuted})";

                if (upToBlock > 0 && blockNumber >= upToBlock) break;
            }

            ulong head = (ulong)blockNumber;
            ulong floor = bundle.NodeServing?.Floor.FloorFor(head)
                          ?? (pathKeyedWindow <= 0 ? 0UL : (head > (ulong)pathKeyedWindow ? head - (ulong)pathKeyedWindow : 0UL));

            var node = BuildNode(bundle);

            return new Replayed(dir)
            {
                Bundle = bundle,
                Node = node,
                Roots = roots.ToArray(),
                Head = head,
                Floor = floor,
                BlockCount = blockNumber,
                Matched = matched,
                FirstMismatch = firstMismatch,
            };
        }

        private static FollowerChainNode BuildNode(RocksDbChainStoreBundle bundle)
        {
            var chainConfig = new ChainConfig
            {
                ChainId = ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = AddressUtil.ZERO_ADDRESS,
                Hardfork = "cancun",
            };
            var hardforkConfig = DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Cancun);
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            var txProcessor = new TransactionProcessor(bundle.State, bundle.Blocks, chainConfig, txVerifier, hardforkConfig);
            return new FollowerChainNode(
                bundle: bundle,
                source: new InertBlockSource(),
                executorFactory: _ => throw new InvalidOperationException("executor not used in this read-only proof driver"),
                policy: new FixedPolicy(),
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                chainConfig: chainConfig,
                hardforkConfig: hardforkConfig,
                txProcessor: txProcessor,
                txVerifier: txVerifier);
        }

        private static async Task LoadGenesisAsync(IStateStore state, string testdata)
        {
            var genesisJson = JObject.Parse(File.ReadAllText(Path.Combine(testdata, "genesis.json")));
            var alloc = (JObject)genesisJson["alloc"];
            foreach (var prop in alloc.Properties())
            {
                var addr = prop.Name.StartsWith("0x") ? prop.Name : "0x" + prop.Name;
                var entry = (JObject)prop.Value;
                var balance = entry["balance"] != null
                    ? new HexBigInteger(entry["balance"].ToString()).Value
                    : BigInteger.Zero;
                ulong nonce = 0;
                if (entry["nonce"] != null)
                {
                    var nVal = new HexBigInteger(entry["nonce"].ToString()).Value;
                    nonce = nVal.IsZero ? 0UL : (ulong)nVal;
                }

                byte[] codeHash = DefaultValues.EMPTY_DATA_HASH;
                if (entry["code"] != null)
                {
                    var code = entry["code"].ToString().HexToByteArray();
                    codeHash = new Nethereum.Util.HashProviders.Sha3KeccackHashProvider().ComputeHash(code);
                    await state.SaveCodeAsync(codeHash, code);
                }

                await state.SaveAccountAsync(addr, new Account
                {
                    Nonce = (EvmUInt256)nonce,
                    Balance = EvmUInt256.FromBigEndian(balance.ToByteArray(isUnsigned: true, isBigEndian: true)),
                    CodeHash = codeHash
                });

                if (entry["storage"] is JObject storage)
                {
                    foreach (var slot in storage.Properties())
                    {
                        var slotKey = new BigInteger(slot.Name.HexToByteArray(), isUnsigned: true, isBigEndian: true);
                        var slotValue = slot.Value.ToString().HexToByteArray();
                        await state.SaveStorageAsync(addr, slotKey, slotValue);
                    }
                }
            }
        }

        private sealed class InertBlockSource : IBlockSource
        {
            public DivergenceSignal LastChainBreak => null;
            public async IAsyncEnumerable<BlockBundle> StreamAsync(ulong fromBlock,
                [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken ct)
            { await Task.CompletedTask; yield break; }
            public Task<BlockSourceHealth> GetHealthAsync(System.Threading.CancellationToken ct) => Task.FromResult(BlockSourceHealth.Healthy);
            public Task ReportBadBundleAsync(ulong b, BadBundleReason r, System.Threading.CancellationToken ct) => Task.CompletedTask;
        }

        private sealed class FixedPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong b) => false;
            public ValidationAction OnVerdict(DivergenceVerdict v, ulong b) => ValidationAction.RewindAndRetry;
        }
    }
}
