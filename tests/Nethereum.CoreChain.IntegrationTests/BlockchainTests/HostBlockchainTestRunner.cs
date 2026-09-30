using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    public class HostBlockchainTestRunner
    {
        public static readonly Nethereum.EVM.HardforkRegistry FixtureRegistry =
            Nethereum.EVM.Precompiles.Bls.Bls12381AwareMainnetHardforkRegistry.Build(
                Nethereum.EVM.Precompiles.Kzg.KzgAwareMainnetHardforkRegistry.Instance,
                new Nethereum.Signer.Bls.Herumi.Bls12381Operations());

        private readonly ITestOutputHelper _output;
        private readonly Sha3Keccack _keccak = new();

        public HostBlockchainTestRunner(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task RunCategoryAsync(string categoryPath, string targetNetwork)
        {
            if (!Directory.Exists(categoryPath))
            {
                _output.WriteLine($"Category not found: {categoryPath}");
                Assert.Fail($"Category not found: {categoryPath}");
                return;
            }

            HardforkName targetFork = HardforkNames.Parse(targetNetwork);

            var testFiles = Directory.GetFiles(categoryPath, "*.json", SearchOption.AllDirectories);
            int totalPassed = 0, totalFailed = 0, totalSkipped = 0;
            var failures = new List<string>();
            var kindCounts = new Dictionary<string, int>();

            foreach (var testFile in testFiles)
            {
                List<BlockchainTestLoader.BlockchainTest> tests;
                try
                {
                    tests = BlockchainTestLoader.LoadFromFile(testFile);
                }
                catch (Exception ex)
                {
                    failures.Add($"{Path.GetFileName(testFile)}: LOAD FAILED — {ex.GetType().Name}: {ex.Message}");
                    Bump(kindCounts, "loadFailure");
                    totalFailed++;
                    continue;
                }

                foreach (var test in tests)
                {
                    HardforkName fork;
                    try
                    {
                        fork = ResolveFork(test);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _output.WriteLine($"  FAIL: {test.Name} — {ex.Message}");
                        failures.Add($"[malformedNetwork] {test.Name}: {ex.Message}");
                        Bump(kindCounts, "malformedNetwork");
                        totalFailed++;
                        continue;
                    }

                    if (fork != targetFork)
                    {
                        totalSkipped++;
                        continue;
                    }

                    TestResult result;
                    try
                    {
                        result = await RunTestAsync(test).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        result = TestResult.Fail("exception", $"{ex.GetType().Name}: {ex.Message}");
                    }

                    if (result.Success)
                    {
                        totalPassed++;
                    }
                    else
                    {
                        var line = $"[{result.Kind}] {test.Name}: {result.Error}";
                        _output.WriteLine($"  FAIL: {line}");
                        failures.Add(line);
                        Bump(kindCounts, result.Kind);
                        totalFailed++;
                    }
                }
            }

            _output.WriteLine("=== Host Blockchain Test SUMMARY ===");
            _output.WriteLine($"Passed: {totalPassed}, Failed: {totalFailed}, Skipped: {totalSkipped}");

            if (kindCounts.Count > 0)
            {
                _output.WriteLine("=== FAILURE KINDS ===");
                foreach (var kv in kindCounts.OrderByDescending(k => k.Value))
                    _output.WriteLine($"  {kv.Key}: {kv.Value}");
            }

            if (failures.Count > 0)
            {
                _output.WriteLine("=== FAILURES ===");
                foreach (var f in failures)
                    _output.WriteLine(f);
            }

            if (totalPassed + totalFailed == 0)
            {
                Assert.Fail($"No test executed for category '{categoryPath}' targeting network '{targetNetwork}': no test matched.");
            }

            Assert.Equal(0, totalFailed);
        }

        public async Task<Nethereum.CoreChain.BlockImporterResult> RunSingleBlockTestAsync(BlockchainTestLoader.BlockchainTest test)
        {
            var stateStore = new InMemoryStateStore();
            var blockStore = new InMemoryBlockStore();
            var trieNodeStore = new InMemoryContentNodeStore();

            var testFork = ResolveFork(test);
            var chainConfig = CreateChainConfig(
                test.ChainId,
                (BigInteger)test.GenesisBlockHeader.GasLimit,
                test.GenesisBlockHeader.BaseFee ?? 0,
                testFork);

            var stateRootCalculator = new Nethereum.CoreChain.IncrementalStateRootCalculator(stateStore, trieNodeStore);

            var engine = new Nethereum.CoreChain.BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(testFork),
                chainConfigFactory: _ => chainConfig,
                hardforkConfigFactory: _ => chainConfig.GetHardforkConfig(),
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: Nethereum.CoreChain.EthereumProofOfWorkRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

            var importer = new Nethereum.CoreChain.BlockImporter(engine, blockStore, stateStore);

            await SeedPreStateAsync(stateStore, test.Pre).ConfigureAwait(false);

            if (test.GenesisBlockHeader.Hash != null && test.GenesisBlockHeader.Hash.Length == 32)
            {
                var genesisHeader = ToModelHeader(test.GenesisBlockHeader);
                await blockStore.SaveAsync(genesisHeader, test.GenesisBlockHeader.Hash).ConfigureAwait(false);
            }

            var blockData = test.Blocks[0];
            var header = ToModelHeader(blockData.BlockHeader);
            var transactions = DecodeTransactionsFromBlockRlp(blockData.Rlp);
            var withdrawals = ConvertWithdrawals(blockData.Withdrawals);

            return await importer.ImportAsync(header, transactions, uncles: null, withdrawals,
                    blockData.BlockAccessList, CancellationToken.None)
                .ConfigureAwait(false);
        }

        public static Nethereum.CoreChain.ChainConfig CreateChainConfig(
            BigInteger chainId, BigInteger blockGasLimit, BigInteger baseFee, HardforkName fork = HardforkName.Amsterdam)
        {
            return new Nethereum.CoreChain.ChainConfig
            {
                ChainId = chainId,
                BlockGasLimit = blockGasLimit,
                BaseFee = baseFee,
                Hardfork = fork.ToString(),
                Registry = FixtureRegistry
            };
        }

        private async Task<TestResult> RunTestAsync(BlockchainTestLoader.BlockchainTest test)
        {
            var stateStore = new InMemoryStateStore();
            var blockStore = new InMemoryBlockStore();
            var trieNodeStore = new InMemoryContentNodeStore();

            var testFork = ResolveFork(test);
            var chainConfig = CreateChainConfig(
                test.ChainId,
                (BigInteger)test.GenesisBlockHeader.GasLimit,
                test.GenesisBlockHeader.BaseFee ?? 0,
                testFork);

            var stateRootCalculator = new Nethereum.CoreChain.IncrementalStateRootCalculator(stateStore, trieNodeStore);

            var engine = new Nethereum.CoreChain.BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(testFork),
                chainConfigFactory: _ => chainConfig,
                hardforkConfigFactory: _ => chainConfig.GetHardforkConfig(),
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: Nethereum.CoreChain.EthereumProofOfWorkRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

            var importer = new Nethereum.CoreChain.BlockImporter(engine, blockStore, stateStore);

            await SeedPreStateAsync(stateStore, test.Pre).ConfigureAwait(false);

            if (test.GenesisBlockHeader.StateRoot != null && test.GenesisBlockHeader.StateRoot.Length > 0)
            {
                var genesisRoot = await stateRootCalculator.ComputeStateRootAsync().ConfigureAwait(false);
                if (!genesisRoot.AreTheSame(test.GenesisBlockHeader.StateRoot))
                {
                    return TestResult.Fail("genesisStateRoot",
                        $"expected=0x{test.GenesisBlockHeader.StateRoot.ToHex()} actual=0x{genesisRoot?.ToHex()}");
                }
            }

            if (test.GenesisBlockHeader.Hash != null && test.GenesisBlockHeader.Hash.Length == 32)
            {
                var genesisHeader = ToModelHeader(test.GenesisBlockHeader);
                await blockStore.SaveAsync(genesisHeader, test.GenesisBlockHeader.Hash).ConfigureAwait(false);
            }

            for (int blockIdx = 0; blockIdx < test.Blocks.Count; blockIdx++)
            {
                var blockData = test.Blocks[blockIdx];
                bool expectingRejection = !string.IsNullOrEmpty(blockData.ExpectException);

                BlockHeader header;
                IList<ISignedTransaction> transactions;
                IList<Withdrawal> withdrawals;

                if (expectingRejection)
                {
                    try
                    {
                        header = DecodeHeaderFromBlockRlp(blockData.Rlp);
                        transactions = DecodeTransactionsFromBlockRlp(blockData.Rlp);
                        withdrawals = DecodeWithdrawalsFromBlockRlp(blockData.Rlp);
                    }
                    catch (Exception ex)
                    {
                        if (!ExpectedRejectionMatcher.Satisfies(blockData.ExpectException, ex, out var decodeDetail))
                        {
                            return TestResult.Fail("rejectionReasonMismatch",
                                $"Block {blockIdx}: expected exception=\"{blockData.ExpectException}\" but the block RLP did not decode — {decodeDetail}");
                        }

                        _output.WriteLine($"  Block {blockIdx}: correctly rejected — undecodable; {decodeDetail}");
                        continue;
                    }
                }
                else
                {
                    header = ToModelHeader(blockData.BlockHeader);
                    transactions = DecodeTransactionsFromBlockRlp(blockData.Rlp);
                    withdrawals = ConvertWithdrawals(blockData.Withdrawals);
                }

                Nethereum.CoreChain.BlockImporterResult result = null;
                Exception thrown = null;
                try
                {
                    result = await importer.ImportAsync(header, transactions, uncles: null, withdrawals,
                            blockData.BlockAccessList, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }

                if (expectingRejection)
                {
                    if (thrown == null && result.RootMatches && result.BlockHash != null)
                    {
                        return TestResult.Fail("rejectionNotEnforced",
                            $"Block {blockIdx} ACCEPTED a block that must be rejected (expected exception=\"{blockData.ExpectException}\")");
                    }

                    var observed = ObservedRejection.FromImport(
                        result?.FailedValidityChecks,
                        result?.InvalidTransactionReason ?? TransactionError.None,
                        thrown ?? result?.Exception);

                    if (!ExpectedRejectionMatcher.Satisfies(blockData.ExpectException, observed, out var rejectionDetail))
                    {
                        return TestResult.Fail("rejectionReasonMismatch",
                            $"Block {blockIdx}: expected exception=\"{blockData.ExpectException}\" but {rejectionDetail}");
                    }

                    _output.WriteLine($"  Block {blockIdx}: correctly rejected — {rejectionDetail}");
                    continue;
                }

                if (thrown != null)
                {
                    return TestResult.Fail("exception", $"Block {blockIdx}: {thrown.GetType().Name}: {thrown.Message}");
                }

                if (!result.RootMatches)
                {
                    var kind = result.FailedChecks.Count > 0 ? string.Join("+", result.FailedChecks) : "unknown";
                    var detail = $"Block {blockIdx}: {string.Join(",", result.FailedChecks)} — expectedStateRoot=0x{result.ExpectedStateRoot?.ToHex()} actualStateRoot=0x{result.ComputedStateRoot?.ToHex()}";

                    if (result.BlockAccessListHashMismatch)
                    {
                        foreach (var line in BlockAccessListDiff.Describe(result.BlockAccessList, blockData.BlockAccessList))
                            _output.WriteLine($"    BAL {line}");

                        detail += $" — BAL {BlockAccessListDiff.Summary(result.BlockAccessList, blockData.BlockAccessList)}";
                    }

                    return TestResult.Fail(kind, detail);
                }

                if (blockData.BlockHeader.Hash != null && blockData.BlockHeader.Hash.Length == 32)
                {
                    var recomputedHash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                    if (!recomputedHash.AreTheSame(blockData.BlockHeader.Hash))
                    {
                        return TestResult.Fail("blockHash",
                            $"Block {blockIdx}: header codec hash mismatch expected=0x{blockData.BlockHeader.Hash.ToHex()} actual=0x{recomputedHash.ToHex()}");
                    }
                }
            }

            return TestResult.Ok();
        }

        private static async Task SeedPreStateAsync(InMemoryStateStore stateStore, Dictionary<string, BlockchainTestLoader.AccountData> pre)
        {
            var keccak = new Sha3Keccack();
            foreach (var kvp in pre)
            {
                var address = kvp.Key;
                var acct = kvp.Value;

                var account = new Account
                {
                    Nonce = acct.Nonce,
                    Balance = acct.Balance
                };

                if (acct.Code != null && acct.Code.Length > 0)
                {
                    var codeHash = keccak.CalculateHash(acct.Code);
                    await stateStore.SaveCodeAsync(codeHash, acct.Code).ConfigureAwait(false);
                    account.CodeHash = codeHash;
                }

                await stateStore.SaveAccountAsync(address, account).ConfigureAwait(false);

                foreach (var storage in acct.Storage)
                {
                    await stateStore.SaveStorageAsync(address, storage.Key, storage.Value.ToBytesForRLPEncoding())
                        .ConfigureAwait(false);
                }
            }
        }

        private static BlockHeader ToModelHeader(BlockchainTestLoader.BlockHeader h)
        {
            return new BlockHeader
            {
                ParentHash = h.ParentHash,
                UnclesHash = h.UncleHash,
                Coinbase = "0x" + h.Coinbase.ToHex(),
                StateRoot = h.StateRoot,
                TransactionsHash = h.TransactionsRoot,
                ReceiptHash = h.ReceiptsRoot,
                LogsBloom = h.LogsBloom,
                Difficulty = new EvmUInt256(h.Difficulty),
                BlockNumber = (long)h.Number,
                GasLimit = (long)h.GasLimit,
                GasUsed = (long)h.GasUsed,
                Timestamp = (long)h.Timestamp,
                ExtraData = h.ExtraData,
                MixHash = h.MixHash,
                Nonce = h.Nonce,
                BaseFee = h.BaseFee.HasValue ? (long?)(long)h.BaseFee.Value : null,
                WithdrawalsRoot = h.WithdrawalsRoot,
                BlobGasUsed = h.BlobGasUsed,
                ExcessBlobGas = h.ExcessBlobGas,
                ParentBeaconBlockRoot = h.ParentBeaconBlockRoot,
                RequestsHash = h.RequestsHash,
                BlockAccessListHash = h.BlockAccessListHash,
                SlotNumber = h.SlotNumber.HasValue ? (ulong?)(ulong)h.SlotNumber.Value : null
            };
        }

        private static IList<Withdrawal> ConvertWithdrawals(List<BlockchainTestLoader.WithdrawalData> withdrawals)
        {
            if (withdrawals == null || withdrawals.Count == 0) return null;
            var result = new List<Withdrawal>(withdrawals.Count);
            foreach (var w in withdrawals)
                result.Add(new Withdrawal
                {
                    Index = w.Index,
                    ValidatorIndex = w.ValidatorIndex,
                    Address = w.Address.HexToByteArray(),
                    AmountInGwei = w.Amount
                });
            return result;
        }

        private static BlockHeader DecodeHeaderFromBlockRlp(byte[] blockRlp)
        {
            var decoded = RLP.RLP.Decode(blockRlp);
            if (!(decoded is RLPCollection blockList) || blockList.Count < 1)
                throw new InvalidOperationException("Block RLP does not contain a header element.");

            var headerRlp = blockList[0].RLPData;
            return BlockHeaderEncoder.Current.Decode(headerRlp);
        }

        private static IList<Withdrawal> DecodeWithdrawalsFromBlockRlp(byte[] blockRlp)
        {
            var decoded = RLP.RLP.Decode(blockRlp);
            if (!(decoded is RLPCollection blockList) || blockList.Count < 4)
                return null;

            var withdrawalList = (RLPCollection)blockList[3];
            var result = new List<Withdrawal>();
            foreach (var wRlp in withdrawalList)
                result.Add(WithdrawalEncoder.Current.Decode(wRlp.RLPData));

            return result;
        }

        private static IList<ISignedTransaction> DecodeTransactionsFromBlockRlp(byte[] blockRlp)
        {
            var result = new List<ISignedTransaction>();
            if (blockRlp == null || blockRlp.Length == 0) return result;

            var decoded = RLP.RLP.Decode(blockRlp);
            if (!(decoded is RLPCollection blockList) || blockList.Count < 2) return result;

            if (!(blockList[1] is RLPCollection txList)) return result;

            foreach (var txItem in txList)
            {
                byte[] txBytes = txItem.RLPData;
                if (txBytes == null || txBytes.Length == 0)
                {
                    if (txItem is RLPCollection txFields)
                    {
                        var fieldBytes = new byte[txFields.Count][];
                        for (int j = 0; j < txFields.Count; j++)
                            fieldBytes[j] = RLP.RLP.EncodeElement(txFields[j].RLPData);
                        txBytes = RLP.RLP.EncodeList(fieldBytes);
                    }
                }

                if (txBytes != null && txBytes.Length > 0)
                    result.Add(TransactionFactory.CreateTransaction(txBytes));
            }

            return result;
        }

        private static HardforkName ResolveFork(BlockchainTestLoader.BlockchainTest test)
        {
            if (string.IsNullOrEmpty(test.Network))
                throw new InvalidOperationException(
                    $"Test '{test.Name}' has no Network field: cannot resolve the hardfork to execute under.");

            try
            {
                return HardforkNames.Parse(test.Network);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(
                    $"Test '{test.Name}' has an unrecognised Network value '{test.Network}': cannot resolve the hardfork to execute under.", ex);
            }
        }

        private static void Bump(Dictionary<string, int> counts, string kind)
        {
            counts.TryGetValue(kind, out var c);
            counts[kind] = c + 1;
        }

        private class TestResult
        {
            public bool Success { get; set; }
            public string Kind { get; set; }
            public string Error { get; set; }

            public static TestResult Ok() => new() { Success = true };
            public static TestResult Fail(string kind, string error) => new() { Success = false, Kind = kind, Error = error };
        }
    }
}
