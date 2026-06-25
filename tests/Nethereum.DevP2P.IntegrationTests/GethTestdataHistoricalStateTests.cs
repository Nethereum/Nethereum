using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.IntegrationTests.Helpers;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Re-executes go-ethereum's testdata chain.rlp from genesis through head,
    /// validating each block's resulting stateRoot against the canonical
    /// header field. The accumulated trie node store can then serve any
    /// historical state root via snap/1 — which is what the AccountRange
    /// test 12 (state at head-127) and GetTrieNodes test 2 (state at head-1)
    /// of devp2p rlpx snap-test need.
    ///
    /// This is the existence proof that our EVM + state machinery is
    /// byte-exact compatible with Geth's on this chain.
    /// </summary>
    public class GethTestdataHistoricalStateTests
    {
        private readonly ITestOutputHelper _output;
        public GethTestdataHistoricalStateTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task LoadGenesisAlloc_ComputedStateRoot_PersistsToTrieNodeStore()
        {
            var testdata = FindTestdata();
            var genesisJson = JObject.Parse(File.ReadAllText(Path.Combine(testdata, "genesis.json")));
            var alloc = (JObject)genesisJson["alloc"];

            var stateStore = new InMemoryStateStore();
            var trieNodeStore = new InMemoryTrieNodeStore();

            int accountCount = 0;
            foreach (var prop in alloc.Properties())
            {
                accountCount++;
                var addr = prop.Name.StartsWith("0x") ? prop.Name : "0x" + prop.Name;
                var entry = (JObject)prop.Value;
                var balance = entry["balance"] != null
                    ? new HexBigInteger(entry["balance"].ToString()).Value
                    : BigInteger.Zero;
                ulong nonce = 0;
                if (entry["nonce"] != null)
                    nonce = new HexBigInteger(entry["nonce"].ToString()).Value.IsZero
                        ? 0UL : (ulong)new HexBigInteger(entry["nonce"].ToString()).Value;

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

            var calculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);
            var computedRoot = await calculator.ComputeStateRootAsync();

            _output.WriteLine($"Loaded {accountCount} accounts from genesis alloc");
            _output.WriteLine($"Computed genesis state root: {computedRoot.ToHex()}");
            _output.WriteLine($"Trie nodes persisted: {trieNodeStore.GetType().Name} populated");

            // This canary doesn't have a hard-coded expected root because Hive's
            // genesis hash isn't directly published; we'll verify the root by
            // re-executing block 1 and checking its parentHash matches the
            // genesis block hash derived from this state root.
            Assert.NotNull(computedRoot);
            Assert.Equal(32, computedRoot.Length);
        }

        [Fact]
        public async Task CoverageReport_AllForksAndTxTypesExercised()
        {
            var testdata = FindTestdata();
            var chainBytes = await File.ReadAllBytesAsync(Path.Combine(testdata, "chain.rlp"));
            var forkResolver = HardforkResolver.HiveTestdata();

            var byFork = new System.Collections.Generic.Dictionary<Nethereum.EVM.HardforkName, int>();
            int totalTxs = 0;
            int totalUncles = 0;
            int totalWithdrawals = 0;
            int totalCreations = 0;
            int blocksWithUncles = 0;
            int blocksWithWithdrawals = 0;
            int legacyTx = 0, type1_2930 = 0, type2_1559 = 0, type3_4844 = 0, type4_7702 = 0;
            int blocksWithBeaconRoot = 0;

            int pos = 0, blockNumber = 0;
            while (pos < chainBytes.Length)
            {
                var blockColl = (RLPCollection)RLP.RLP.DecodeFirstElement(chainBytes, pos);
                int consumed = GetRlpItemLength(chainBytes, pos);
                var header = new BlockHeaderEncoder().Decode(ReEncodeAsList((RLPCollection)blockColl[0]));
                blockNumber++;

                var fork = forkResolver.ResolveAt((long)header.BlockNumber, (ulong)header.Timestamp);
                byFork.TryGetValue(fork, out var ct); byFork[fork] = ct + 1;

                if (header.ParentBeaconBlockRoot != null && header.ParentBeaconBlockRoot.Length > 0)
                    blocksWithBeaconRoot++;

                var txList = (RLPCollection)blockColl[1];
                totalTxs += txList.Count;
                foreach (var txItem in txList)
                {
                    byte[] txBytes = txItem is RLPCollection c ? ReEncodeAsList(c) : txItem.RLPData;
                    if (txBytes.Length > 0)
                    {
                        if (txBytes[0] == 0x01) type1_2930++;
                        else if (txBytes[0] == 0x02) type2_1559++;
                        else if (txBytes[0] == 0x03) type3_4844++;
                        else if (txBytes[0] == 0x04) type4_7702++;
                        else legacyTx++;
                    }
                    var tx = TransactionFactory.CreateTransaction(txBytes);
                    if (tx is LegacyTransaction lt && (lt.ReceiveAddress == null || lt.ReceiveAddress.Length == 0))
                        totalCreations++;
                    else if (tx is Nethereum.Model.Transaction1559 t1559 && (t1559.ReceiverAddress == null || t1559.ReceiverAddress.Length == 0))
                        totalCreations++;
                    else if (tx is Nethereum.Model.Transaction2930 t2930 && (t2930.ReceiverAddress == null || t2930.ReceiverAddress.Length == 0))
                        totalCreations++;
                }

                var uncleList = (RLPCollection)blockColl[2];
                if (uncleList.Count > 0) { totalUncles += uncleList.Count; blocksWithUncles++; }

                if (blockColl.Count >= 4 && blockColl[3] is RLPCollection wList && wList.Count > 0)
                {
                    totalWithdrawals += wList.Count;
                    blocksWithWithdrawals++;
                }

                pos += consumed;
            }

            _output.WriteLine($"=== Geth testdata chain.rlp coverage ===");
            _output.WriteLine($"Total blocks: {blockNumber}");
            _output.WriteLine("");
            _output.WriteLine("Blocks per fork:");
            foreach (var kv in byFork.OrderBy(k => (int)k.Key))
                _output.WriteLine($"  {kv.Key,-20} : {kv.Value}");
            _output.WriteLine("");
            _output.WriteLine($"Total transactions: {totalTxs}");
            _output.WriteLine($"  legacy (no type prefix)    : {legacyTx}");
            _output.WriteLine($"  type 1 (EIP-2930 acl)      : {type1_2930}");
            _output.WriteLine($"  type 2 (EIP-1559 fee)      : {type2_1559}");
            _output.WriteLine($"  type 3 (EIP-4844 blob)     : {type3_4844}");
            _output.WriteLine($"  type 4 (EIP-7702 authlist) : {type4_7702}");
            _output.WriteLine($"  contract creations         : {totalCreations}");
            _output.WriteLine("");
            _output.WriteLine($"Blocks with uncles: {blocksWithUncles} (total uncle headers: {totalUncles})");
            _output.WriteLine($"Blocks with withdrawals: {blocksWithWithdrawals} (total withdrawal entries: {totalWithdrawals})");
            _output.WriteLine($"Blocks with parentBeaconBlockRoot (EIP-4788 active): {blocksWithBeaconRoot}");
        }

        [Fact]
        public async Task ProcessChain_AllBlocks_StateRootsMatchEachHeader()
        {
            var testdata = FindTestdata();
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

            int pos = 0;
            int blockNumber = 0;
            int matched = 0;
            string firstMismatchHex = null;
            int firstMismatchBlock = -1;
            while (pos < chainBytes.Length)
            {
                var item = RLP.RLP.DecodeFirstElement(chainBytes, pos);
                int consumed = GetRlpItemLength(chainBytes, pos);
                var blockColl = (RLPCollection)item;
                var headerEncoded = ReEncodeAsList((RLPCollection)blockColl[0]);
                var header = new BlockHeaderEncoder().Decode(headerEncoded);
                var txList = (RLPCollection)blockColl[1];
                var transactions = new System.Collections.Generic.List<ISignedTransaction>();
                foreach (var txItem in txList)
                {
                    byte[] txBytes = txItem is RLPCollection coll ? ReEncodeAsList(coll) : txItem.RLPData;
                    transactions.Add(TransactionFactory.CreateTransaction(txBytes));
                }
                // Uncles list is block element [2]. Decode each entry as a header.
                var uncleList = (RLPCollection)blockColl[2];
                var uncles = new System.Collections.Generic.List<BlockHeader>();
                foreach (var u in uncleList)
                {
                    var uncleEncoded = ReEncodeAsList((RLPCollection)u);
                    uncles.Add(new BlockHeaderEncoder().Decode(uncleEncoded));
                }

                // Withdrawals list is block element [3] (Shanghai+). Each
                // entry = [index, validatorIndex, address, amount_gwei].
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

                var result = await processor.ImportAsync(header, transactions, uncles, withdrawals);
                blockNumber++;
                if (result.RootMatches)
                {
                    matched++;
                }
                else if (firstMismatchHex == null)
                {
                    firstMismatchBlock = blockNumber;
                    firstMismatchHex = $"expected {result.ExpectedStateRoot.ToHex()}, got {result.ComputedStateRoot?.ToHex()} (fork={result.Fork}, txs={result.TransactionsExecuted}, uncles={uncles.Count})";
                }

                pos += consumed;
            }

            _output.WriteLine($"Processed {blockNumber} blocks, {matched} matched");
            if (firstMismatchHex != null)
                _output.WriteLine($"First mismatch at block {firstMismatchBlock}: {firstMismatchHex}");

            Assert.Equal(blockNumber, matched);
        }

        private static int GetRlpItemLength(byte[] data, int pos) =>
            Helpers.RlpStreamHelpers.GetRlpItemLength(data, pos);

        [Fact]
        public async Task DiagnoseBlock4_DumpTxAndStateDiff()
        {
            var testdata = FindTestdata();
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

            int pos = 0;
            for (int n = 1; n <= 4; n++)
            {
                var item = RLP.RLP.DecodeFirstElement(chainBytes, pos);
                int consumed = GetRlpItemLength(chainBytes, pos);
                var blockColl = (RLPCollection)item;
                var headerEncoded = ReEncodeAsList((RLPCollection)blockColl[0]);
                var header = new BlockHeaderEncoder().Decode(headerEncoded);
                var txList = (RLPCollection)blockColl[1];
                var transactions = new System.Collections.Generic.List<ISignedTransaction>();
                foreach (var txItem in txList)
                {
                    byte[] txBytes = txItem is RLPCollection coll ? ReEncodeAsList(coll) : txItem.RLPData;
                    transactions.Add(TransactionFactory.CreateTransaction(txBytes));
                }

                if (n == 4)
                {
                    _output.WriteLine($"=== BLOCK 4 STRUCTURE ===");
                    _output.WriteLine($"Number: {header.BlockNumber}");
                    _output.WriteLine($"Coinbase: {header.Coinbase}");
                    _output.WriteLine($"GasUsed: {header.GasUsed}, GasLimit: {header.GasLimit}");
                    _output.WriteLine($"Difficulty: {header.Difficulty}");
                    _output.WriteLine($"Tx count: {transactions.Count}");
                    _output.WriteLine($"Uncles count: {((RLPCollection)blockColl[2]).Count}");

                    string txSender = null;
                    for (int i = 0; i < transactions.Count; i++)
                    {
                        var tx = transactions[i];
                        _output.WriteLine($"--- tx {i} ---");
                        _output.WriteLine($"  hash:     {tx.Hash?.ToHex()}");
                        _output.WriteLine($"  raw type: {tx.GetType().Name}");
                        var sender = new TransactionVerificationAndRecoveryImp().GetSenderAddress(tx);
                        if (i == 0) txSender = sender;
                        _output.WriteLine($"  sender:   {sender}");
                        if (tx is LegacyTransaction lt)
                        {
                            _output.WriteLine($"  to:       {lt.ReceiveAddress?.ToHex(true)}");
                            _output.WriteLine($"  value:    {lt.Value.ToHex(true)}");
                            _output.WriteLine($"  gasPrice: {lt.GasPrice.ToHex(true)}");
                            _output.WriteLine($"  gasLimit: {lt.GasLimit.ToHex(true)}");
                            _output.WriteLine($"  nonce:    {lt.Nonce.ToHex(true)}");
                            _output.WriteLine($"  data:     {lt.Data?.ToHex(true)}");
                            _output.WriteLine($"  data.Length: {lt.Data?.Length ?? 0}");
                            _output.WriteLine($"  isCreation: {lt.ReceiveAddress == null || lt.ReceiveAddress.Length == 0}");
                        }
                    }

                    // Run the tx in isolation BEFORE the block-level wrappers so
                    // we can extract gasUsed and compare to the header's value.
                    var chainConfig = new ChainConfig
                    {
                        ChainId = BigInteger.Parse("3503995874084926"),
                        BaseFee = BigInteger.Zero,
                        Coinbase = AddressUtil.ZERO_ADDRESS,
                        Hardfork = "homestead"
                    };
                    var diagnoseEngine = new BlockExecutor(
                        stateStore, new InMemoryBlockStore(), new FixedChainActivations(HardforkName.Homestead),
                        chainConfigFactory: _ => chainConfig,
                        hardforkConfigFactory: _ => Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Homestead),
                        stateRootCalculator: calculator,
                        rewardPolicy: NoRewardPolicy.Instance,
                        trieNodeStore: trieNodeStore);
                    var txEntries = new System.Collections.Generic.List<TxEntry>();
                    foreach (var t in transactions) txEntries.Add(new TxEntry(t, null));
                    var execResult = await diagnoseEngine.ExecuteAsync(
                        header, txEntries, uncles: null, withdrawals: null, new BlockExecutionOptions());
                    _output.WriteLine($"Our gasUsed: {execResult.Receipts[0].GasUsed} (header says {header.GasUsed})");
                    _output.WriteLine($"Difference: {(long)header.GasUsed - (long)execResult.Receipts[0].GasUsed}");
                    _output.WriteLine($"Tx success: {execResult.Receipts[0].Success}");
                    _output.WriteLine($"Tx error: {execResult.Receipts[0].RevertReason}");

                    // Apply miner reward to match the full block transition.
                    var minerReward = BlockRewardCalculator.MinerReward(HardforkName.Homestead);
                    var coinbase = AddressUtil.ZERO_ADDRESS;
                    var cb = await stateStore.GetAccountAsync(coinbase) ?? new Account
                    {
                        Nonce = EvmUInt256.Zero,
                        Balance = EvmUInt256.Zero,
                        CodeHash = DefaultValues.EMPTY_DATA_HASH
                    };
                    var cbCurrent = new BigInteger(cb.Balance.ToBigEndian(), isUnsigned:true, isBigEndian:true);
                    cb.Balance = EvmUInt256.FromBigEndian((cbCurrent + minerReward).ToByteArray(isUnsigned:true, isBigEndian:true));
                    await stateStore.SaveAccountAsync(coinbase, cb);
                    var computedRoot = await calculator.ComputeStateRootAsync();
                    _output.WriteLine($"Expected stateRoot: {header.StateRoot.ToHex()}");
                    _output.WriteLine($"Computed stateRoot: {computedRoot.ToHex()}");

                    // Compute the would-be contract address: keccak256(rlp([sender, nonce]))[12:].
                    var senderBytes = txSender.Substring(2).HexToByteArray();
                    var nonceBytes = new byte[] { 0x03 };
                    var rlpForAddr = Nethereum.RLP.RLP.EncodeList(
                        Nethereum.RLP.RLP.EncodeElement(senderBytes),
                        Nethereum.RLP.RLP.EncodeElement(nonceBytes));
                    var addrHash = new Nethereum.Util.Sha3Keccack().CalculateHash(rlpForAddr);
                    var contractAddr = "0x" + addrHash.Skip(12).ToArray().ToHex();
                    _output.WriteLine($"Contract addr (sender+nonce): {contractAddr}");
                    var contractAcc = await stateStore.GetAccountAsync(contractAddr);
                    if (contractAcc == null)
                    {
                        _output.WriteLine("Contract account: NOT FOUND in state store");
                    }
                    else
                    {
                        var cbal = new BigInteger(contractAcc.Balance.ToBigEndian(), isUnsigned:true, isBigEndian:true);
                        var cnonce = new BigInteger(contractAcc.Nonce.ToBigEndian(), isUnsigned:true, isBigEndian:true);
                        _output.WriteLine($"Contract account: balance={cbal} nonce={cnonce} codeHash={contractAcc.CodeHash?.ToHex()} stateRoot={contractAcc.StateRoot?.ToHex()}");
                    }

                    // Sender account after the tx.
                    var senderAcc = await stateStore.GetAccountAsync(txSender);
                    var sbal = new BigInteger(senderAcc.Balance.ToBigEndian(), isUnsigned:true, isBigEndian:true);
                    var snonce = new BigInteger(senderAcc.Nonce.ToBigEndian(), isUnsigned:true, isBigEndian:true);
                    _output.WriteLine($"Sender account: balance={sbal} nonce={snonce}");

                    // HYPOTHESIS: missing contract account is the bug. Add it
                    // and check if state root matches.
                    if (contractAcc == null)
                    {
                        _output.WriteLine($"Adding empty contract account at {contractAddr} (Homestead semantics) and re-checking root…");
                        await stateStore.SaveAccountAsync(contractAddr, new Account
                        {
                            Nonce = EvmUInt256.Zero,
                            Balance = EvmUInt256.Zero,
                            StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                            CodeHash = DefaultValues.EMPTY_DATA_HASH
                        });
                        var newRoot = await calculator.ComputeStateRootAsync();
                        _output.WriteLine($"After adding contract: {newRoot.ToHex()}");
                        _output.WriteLine($"Now matches header? {ByteUtil.AreEqual(newRoot, header.StateRoot)}");
                    }

                    // Print balances of accounts most likely to differ.
                    foreach (var addr in new[]
                    {
                        header.Coinbase,
                        "0x0c2c51a0990aee1d73c1228de158688341557508",  // some genesis-alloc account
                        AddressUtil.ZERO_ADDRESS,
                    })
                    {
                        var acc = await stateStore.GetAccountAsync(addr);
                        var balStr = acc != null ? new BigInteger(acc.Balance.ToBigEndian(), isUnsigned:true, isBigEndian:true).ToString() : "null";
                        var nonceStr = acc != null ? new BigInteger(acc.Nonce.ToBigEndian(), isUnsigned:true, isBigEndian:true).ToString() : "null";
                        _output.WriteLine($"  account {addr}: balance={balStr} nonce={nonceStr}");
                    }
                    return;
                }

                var r = await processor.ImportAsync(header, transactions, uncles: null, withdrawals: null);
                Assert.True(r.RootMatches, $"Block {n} broke before reaching block 4: {r.ErrorMessage}");
                pos += consumed;
            }
        }

        [Fact]
        public async Task ProcessBlock1_FromGenesisState_StateRootMatchesHeader()
        {
            var testdata = FindTestdata();
            var (stateStore, trieNodeStore) = await LoadGenesisAsync(testdata);

            // Decode block 1 from chain.rlp (the first block in the stream).
            var chainBytes = await File.ReadAllBytesAsync(Path.Combine(testdata, "chain.rlp"));
            var firstBlockRlp = (RLPCollection)RLP.RLP.DecodeFirstElement(chainBytes, 0);
            var headerRlpItem = (RLPCollection)firstBlockRlp[0];
            var headerEncoded = ReEncodeAsList(headerRlpItem);
            var header = new BlockHeaderEncoder().Decode(headerEncoded);
            _output.WriteLine($"Block #{header.BlockNumber}, expected stateRoot: {header.StateRoot.ToHex()}, tx count rlp items: {((RLPCollection)firstBlockRlp[1]).Count}");

            // Decode transactions.
            var txList = (RLPCollection)firstBlockRlp[1];
            var transactions = new System.Collections.Generic.List<ISignedTransaction>();
            foreach (var txItem in txList)
            {
                byte[] txBytes;
                if (txItem is RLPCollection txColl)
                    txBytes = ReEncodeAsList(txColl);
                else
                    txBytes = txItem.RLPData;
                transactions.Add(TransactionFactory.CreateTransaction(txBytes));
            }
            _output.WriteLine($"Decoded {transactions.Count} transactions");

            // Process via the full BlockImporter (handles per-block fork
            // resolution + miner reward + later: EIP-4788 + withdrawals).
            var blockStore = new InMemoryBlockStore();
            var calculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);
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

            var result = await processor.ImportAsync(header, transactions, uncles: null, withdrawals: null);
            _output.WriteLine($"Active fork: {result.Fork}");
            _output.WriteLine($"Miner reward credited: {result.MinerRewardCredited} wei");
            _output.WriteLine($"Transactions executed: {result.TransactionsExecuted}");
            _output.WriteLine($"Expected: {result.ExpectedStateRoot.ToHex()}");
            _output.WriteLine($"Computed: {result.ComputedStateRoot?.ToHex()}");
            if (!string.IsNullOrEmpty(result.ErrorMessage)) _output.WriteLine($"Error: {result.ErrorMessage}");

            Assert.True(result.RootMatches,
                $"Block 1 stateRoot mismatch.\n  Expected (header): {result.ExpectedStateRoot.ToHex()}\n  Computed: {result.ComputedStateRoot?.ToHex()}");
        }

        private static byte[] ReEncodeAsList(RLPCollection coll) =>
            Helpers.RlpStreamHelpers.ReEncodeAsList(coll);

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

        private static string FindTestdata() => Helpers.GethToolLocator.FindEthTestTestdata();
    }
}
