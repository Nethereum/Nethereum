using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.IntegrationTests.Helpers;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;
using Newtonsoft.Json.Linq;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Builds the full historical state of Geth's testdata chain by replaying
    /// genesis + 500 blocks through our <see cref="BlockImporter"/>. The
    /// resulting <see cref="ITrieNodeStore"/> contains every trie node ever
    /// created across the chain — sufficient to serve a snap server at any
    /// historical state root (head − 1, head − 127, …).
    ///
    /// Cached statically because each call costs ~2 seconds of EVM execution.
    /// </summary>
    public static class GethTestdataHistoricalStateBuilder
    {
        public class Result
        {
            public ITrieStorage TrieStorage { get; set; }
            public IBytecodeStore Bytecodes { get; set; }
            public IStateStore HeadState { get; set; }
            public byte[] HeadStateRoot { get; set; }
            public int BlocksProcessed { get; set; }
            public int BlocksMatched { get; set; }
        }

        private class StateStoreBytecodeAdapter : IBytecodeStore
        {
            private readonly IStateStore _stateStore;
            public StateStoreBytecodeAdapter(IStateStore stateStore) { _stateStore = stateStore; }
            public byte[] Get(byte[] codeHash)
            {
                // Snap server expects synchronous access; InMemoryStateStore
                // resolves immediately via a ConcurrentDictionary so the .Result
                // here doesn't actually block.
                return _stateStore.GetCodeAsync(codeHash).GetAwaiter().GetResult();
            }
        }

        private static Result _cached;
        private static readonly object _lock = new();

        public static Result Build(string testdata)
        {
            lock (_lock)
            {
                if (_cached != null) return _cached;
                _cached = BuildAsync(testdata).GetAwaiter().GetResult();
                return _cached;
            }
        }

        private static async Task<Result> BuildAsync(string testdata)
        {
            var (stateStore, trieNodeStore) = await LoadGenesisAsync(testdata);
            var chainBytes = await File.ReadAllBytesAsync(Path.Combine(testdata, "chain.rlp"));
            var calculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);
            var blockStore = new InMemoryBlockStore();
            var activations = HiveTestdataChainActivations.Instance;
            var engine = new BlockExecutor(
                stateStore, blockStore, activations,
                chainConfigFactory: f => new ChainConfig
                {
                    ChainId = BigInteger.Parse("3503995874084926"),
                    BaseFee = BigInteger.Zero,
                    Coinbase = AddressUtil.ZERO_ADDRESS,
                    Hardfork = f.ToString().ToLowerInvariant()
                },
                hardforkConfigFactory: f => Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance.Get(f),
                stateRootCalculator: calculator,
                rewardPolicy: EthereumProofOfWorkRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);
            var processor = new BlockImporter(engine, blockStore, stateStore);

            int pos = 0, blockNumber = 0, matched = 0;
            byte[] finalRoot = null;
            while (pos < chainBytes.Length)
            {
                var blockColl = (RLPCollection)RLP.RLP.DecodeFirstElement(chainBytes, pos);
                int consumed = Helpers.RlpStreamHelpers.GetRlpItemLength(chainBytes, pos);

                var header = new BlockHeaderEncoder().Decode(Helpers.RlpStreamHelpers.ReEncodeAsList((RLPCollection)blockColl[0]));
                var txList = (RLPCollection)blockColl[1];
                var transactions = new System.Collections.Generic.List<ISignedTransaction>();
                foreach (var txItem in txList)
                {
                    byte[] txBytes = txItem is RLPCollection c ? Helpers.RlpStreamHelpers.ReEncodeAsList(c) : txItem.RLPData;
                    transactions.Add(TransactionFactory.CreateTransaction(txBytes));
                }
                var uncleList = (RLPCollection)blockColl[2];
                var uncles = new System.Collections.Generic.List<BlockHeader>();
                foreach (var u in uncleList)
                    uncles.Add(new BlockHeaderEncoder().Decode(Helpers.RlpStreamHelpers.ReEncodeAsList((RLPCollection)u)));
                var withdrawals = new System.Collections.Generic.List<WithdrawalEntry>();
                if (blockColl.Count >= 4 && blockColl[3] is RLPCollection wList)
                {
                    foreach (var wItem in wList)
                    {
                        var wColl = (RLPCollection)wItem;
                        var addr = "0x" + wColl[2].RLPData.ToHex();
                        var amount = wColl[3].RLPData == null || wColl[3].RLPData.Length == 0
                            ? BigInteger.Zero
                            : wColl[3].RLPData.ToBigIntegerFromRLPDecoded();
                        withdrawals.Add(new WithdrawalEntry(addr, amount));
                    }
                }

                var blockResult = await processor.ImportAsync(header, transactions, uncles, withdrawals);
                blockNumber++;
                if (blockResult.RootMatches) matched++;
                finalRoot = blockResult.ComputedStateRoot;

                pos += consumed;
            }

            return new Result
            {
                TrieStorage = trieNodeStore,
                Bytecodes = new StateStoreBytecodeAdapter(stateStore),
                HeadState = stateStore,
                HeadStateRoot = finalRoot,
                BlocksProcessed = blockNumber,
                BlocksMatched = matched
            };
        }

        private static async Task<(InMemoryStateStore stateStore, InMemoryTrieNodeStore trieNodeStore)>
            LoadGenesisAsync(string testdata)
        {
            var genesisJson = JObject.Parse(File.ReadAllText(Path.Combine(testdata, "genesis.json")));
            var alloc = (JObject)genesisJson["alloc"];
            var stateStore = new InMemoryStateStore();
            var trieNodeStore = new InMemoryTrieNodeStore();
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
                    var keccak = new Nethereum.Util.HashProviders.Sha3KeccackHashProvider();
                    codeHash = keccak.ComputeHash(code);
                    await stateStore.SaveCodeAsync(codeHash, code);
                }
                await stateStore.SaveAccountAsync(addr, new Account
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
                        await stateStore.SaveStorageAsync(addr, slotKey, slotValue);
                    }
                }
            }
            return (stateStore, trieNodeStore);
        }
    }
}
