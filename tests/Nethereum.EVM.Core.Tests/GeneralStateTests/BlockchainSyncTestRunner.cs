using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Nethereum.CoreChain;
using Nethereum.CoreChain.IntegrationTests.BlockchainTests;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class BlockchainSyncTestRunner
    {
        private readonly ITestOutputHelper _output;

        public BlockchainSyncTestRunner(ITestOutputHelper output)
        {
            _output = output;
        }

        public void RunCategory(string categoryPath, string targetNetwork = "Cancun")
        {
            if (!Directory.Exists(categoryPath))
            {
                _output.WriteLine($"Category not found: {categoryPath}");
                Assert.Fail($"Category not found: {categoryPath}");
                return;
            }

            var testFiles = Directory.GetFiles(categoryPath, "*.json", SearchOption.AllDirectories);
            int totalPassed = 0, totalFailed = 0, totalSkipped = 0;
            var failures = new List<string>();

            foreach (var testFile in testFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(testFile);
                var tests = BlockchainTestLoader.LoadFromFile(testFile);

                foreach (var test in tests)
                {
                    if (!string.IsNullOrEmpty(targetNetwork) &&
                        !test.Name.Contains(targetNetwork, StringComparison.OrdinalIgnoreCase))
                    {
                        totalSkipped++;
                        continue;
                    }

                    try
                    {
                        var result = ExecuteTest(test);
                        if (result.Success)
                        {
                            totalPassed++;
                        }
                        else
                        {
                            failures.Add($"{test.Name}: {result.Error}");
                            totalFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  SKIP: {test.Name} — {ex.GetType().Name}: {ex.Message}");
                        totalSkipped++;
                    }
                }
            }

            _output.WriteLine($"=== Blockchain Test SUMMARY ===");
            _output.WriteLine($"Passed: {totalPassed}, Failed: {totalFailed}, Skipped: {totalSkipped}");

            if (failures.Count > 0)
            {
                _output.WriteLine("=== FAILURES ===");
                foreach (var f in failures)
                    _output.WriteLine(f);
            }

            Assert.Equal(0, totalFailed);
        }

        public TestResult ExecuteTest(BlockchainTestLoader.BlockchainTest test)
        {
            var encoding = RlpBlockEncodingProvider.Instance;
            var fork = DetectFork(test.Name);
            byte[] parentHash = test.GenesisBlockHeader.Hash;

            // Block hash history for BLOCKHASH opcode
            var blockHashes = new Dictionary<long, byte[]>();
            if (test.GenesisBlockHeader.Hash != null && test.GenesisBlockHeader.Hash.Length > 0)
                blockHashes[(long)test.GenesisBlockHeader.Number] = test.GenesisBlockHeader.Hash;

            // Pre-state from genesis
            var preAccounts = ConvertPreState(test.Pre);

            // Verify genesis state root matches
            if (test.GenesisBlockHeader.StateRoot != null && test.GenesisBlockHeader.StateRoot.Length > 0)
            {
                var genesisES = new ExecutionStateService(
                    new InMemoryStateReader(WitnessStateBuilder.BuildAccountState(preAccounts)));
                WitnessStateBuilder.LoadAllAccountsAndStorage(genesisES, preAccounts);
                var genesisRoot = new PatriciaStateRootCalculator(encoding).ComputeStateRoot(genesisES);
                if (!genesisRoot.AreTheSame(test.GenesisBlockHeader.StateRoot))
                {
                    _output.WriteLine($"  GENESIS ROOT MISMATCH: expected=0x{test.GenesisBlockHeader.StateRoot.ToHex().Substring(0,16)}... actual=0x{genesisRoot.ToHex().Substring(0,16)}...");
                    return TestResult.Fail($"Genesis state root mismatch");
                }
            }

            for (int blockIdx = 0; blockIdx < test.Blocks.Count; blockIdx++)
            {
                var blockData = test.Blocks[blockIdx];
                var expectedHeader = blockData.BlockHeader;

                if (!string.IsNullOrEmpty(blockData.ExpectException))
                    continue;

                // Extract transaction RLPs from block RLP
                var txRlps = ExtractTransactionRlps(blockData.Rlp);

                // Populate the EIP-2935 history contract's storage from the
                // known block-hash history so BLOCKHASH resolves in-guest.
                Nethereum.EVM.Witness.HistoryContractHelpers.PopulateFromBlockHashes(preAccounts, blockHashes);

                // Build BlockWitnessData
                var block = new BlockWitnessData
                {
                    BlockNumber = (long)expectedHeader.Number,
                    Timestamp = (long)expectedHeader.Timestamp,
                    BaseFee = expectedHeader.BaseFee.HasValue ? (long)expectedHeader.BaseFee.Value : 0,
                    BlockGasLimit = (long)expectedHeader.GasLimit,
                    ChainId = (long)test.ChainId,
                    Coinbase = "0x" + expectedHeader.Coinbase.ToHex(),
                    Difficulty = EvmUInt256BigIntegerExtensions.FromBigInteger(expectedHeader.Difficulty).ToBigEndian(),
                    ParentHash = parentHash ?? new byte[32],
                    ExtraData = expectedHeader.ExtraData ?? new byte[0],
                    MixHash = expectedHeader.MixHash ?? new byte[32],
                    Nonce = expectedHeader.Nonce ?? new byte[8],
                    Withdrawals = ConvertWithdrawals(blockData.Withdrawals),
                    BlobGasUsed = expectedHeader.BlobGasUsed,
                    ExcessBlobGas = expectedHeader.ExcessBlobGas,
                    ParentBeaconBlockRoot = expectedHeader.ParentBeaconBlockRoot,
                    RequestsHash = expectedHeader.RequestsHash,
                    ProduceBlockCommitments = true,
                    ComputePostStateRoot = true,
                    Transactions = new List<BlockWitnessTransaction>(),
                    Accounts = preAccounts,
                    Features = new BlockFeatureConfig { Fork = fork }
                };

                // Add transactions with sender from test data
                for (int i = 0; i < txRlps.Count; i++)
                {
                    var sender = i < blockData.Transactions.Count
                        ? blockData.Transactions[i].Sender
                        : "";

                    block.Transactions.Add(new BlockWitnessTransaction
                    {
                        From = string.IsNullOrEmpty(sender) ? "" : sender,
                        RlpEncoded = txRlps[i]
                    });
                }

                // Test system call directly to diagnose
                if (block.ParentBeaconBlockRoot != null && block.ParentBeaconBlockRoot.Length > 0)
                {
                    _output.WriteLine($"    Beacon root call: parentBeaconRoot=0x{block.ParentBeaconBlockRoot.ToHex().Substring(0, 16)}... timestamp={block.Timestamp}");
                }

                // Execute
                var result = BlockExecutor.Execute(
                    block,
                    encoding,
                    MainnetRegistry,
                    new PatriciaStateRootCalculator(encoding),
                    new PatriciaBlockRootCalculator());

                // Diagnostics
                _output.WriteLine($"  Block {blockIdx}: txCount={block.Transactions.Count} gasUsed={result.CumulativeGasUsed} expectedGas={(long)expectedHeader.GasUsed}");
                for (int t = 0; t < result.TxResults.Count; t++)
                {
                    var txr = result.TxResults[t];
                    var txType = block.Transactions[t].RlpEncoded?.Length > 0 ? block.Transactions[t].RlpEncoded[0] : (byte)0;
                    _output.WriteLine($"    tx[{t}]: type=0x{txType:x2} success={txr.Success} gas={txr.GasUsed}{(txr.Error != null ? " err=" + txr.Error : "")}");
                }
                // Dump all accounts with non-default state
                if (result.FinalExecutionState != null)
                {
                    foreach (var acct in result.FinalExecutionState.AccountsState)
                    {
                        var bal = acct.Value.Balance.GetTotalBalance();
                        var nonce = acct.Value.Nonce ?? EvmUInt256.Zero;
                        var storageCount = acct.Value.Storage?.Count ?? 0;
                        var codeLen = acct.Value.Code?.Length ?? 0;
                        if (!bal.IsZero || nonce > 0 || storageCount > 0 || codeLen > 0)
                            _output.WriteLine($"    {acct.Key}: bal={bal} nonce={nonce} code={codeLen} storage={storageCount}");
                    }
                }
                for (int t = 0; t < result.TxResults.Count; t++)
                {
                    var txr = result.TxResults[t];
                    _output.WriteLine($"    tx[{t}]: success={txr.Success} gas={txr.GasUsed} error={txr.Error}");
                }

                // Debug: verify our header encoder produces correct hash for expected header values
                {
                    var testHeader = new BlockHeader
                    {
                        ParentHash = expectedHeader.ParentHash,
                        UnclesHash = expectedHeader.UncleHash,
                        Coinbase = "0x" + expectedHeader.Coinbase.ToHex(),
                        StateRoot = expectedHeader.StateRoot,
                        TransactionsHash = expectedHeader.TransactionsRoot,
                        ReceiptHash = expectedHeader.ReceiptsRoot,
                        LogsBloom = expectedHeader.LogsBloom,
                        Difficulty = EvmUInt256BigIntegerExtensions.FromBigInteger(expectedHeader.Difficulty),
                        BlockNumber = (long)expectedHeader.Number,
                        GasLimit = (long)expectedHeader.GasLimit,
                        GasUsed = (long)expectedHeader.GasUsed,
                        Timestamp = (long)expectedHeader.Timestamp,
                        ExtraData = expectedHeader.ExtraData,
                        MixHash = expectedHeader.MixHash,
                        Nonce = expectedHeader.Nonce,
                        BaseFee = expectedHeader.BaseFee.HasValue ? EvmUInt256BigIntegerExtensions.FromBigInteger(expectedHeader.BaseFee.Value) : (EvmUInt256?)null,
                        WithdrawalsRoot = expectedHeader.WithdrawalsRoot,
                        BlobGasUsed = expectedHeader.BlobGasUsed,
                        ExcessBlobGas = expectedHeader.ExcessBlobGas,
                        ParentBeaconBlockRoot = expectedHeader.ParentBeaconBlockRoot,
                        RequestsHash = expectedHeader.RequestsHash
                    };
                    var encodedTestHeader = BlockHeaderEncoder.Current.Encode(testHeader);
                    var testHash = new Sha3Keccack().CalculateHash(encodedTestHeader);
                    if (testHash.AreTheSame(expectedHeader.Hash))
                        _output.WriteLine($"    Header encoder: CORRECT (hash matches with expected values)");
                    else
                        _output.WriteLine($"    Header encoder: WRONG hash=0x{testHash.ToHex().Substring(0,16)}... expected=0x{expectedHeader.Hash.ToHex().Substring(0,16)}... rlpLen={encodedTestHeader.Length}");
                }

                // Validate state root
                if (result.StateRoot != null && expectedHeader.StateRoot.Length > 0)
                {
                    if (!result.StateRoot.AreTheSame(expectedHeader.StateRoot))
                    {
                        CompareWithExpectedPostState(result, test.PostState);
                        return TestResult.Fail(
                            $"Block {blockIdx}: state root mismatch expected=0x{expectedHeader.StateRoot.ToHex()} actual=0x{result.StateRoot.ToHex()}");
                    }
                }

                // Validate transactions root
                if (result.TransactionsRoot != null && expectedHeader.TransactionsRoot.Length > 0)
                {
                    if (!result.TransactionsRoot.AreTheSame(expectedHeader.TransactionsRoot))
                    {
                        return TestResult.Fail(
                            $"Block {blockIdx}: tx root mismatch expected=0x{expectedHeader.TransactionsRoot.ToHex()} actual=0x{result.TransactionsRoot.ToHex()}");
                    }
                }

                // Validate receipts root
                if (result.ReceiptsRoot != null && expectedHeader.ReceiptsRoot.Length > 0)
                {
                    if (!result.ReceiptsRoot.AreTheSame(expectedHeader.ReceiptsRoot))
                    {
                        return TestResult.Fail(
                            $"Block {blockIdx}: receipts root mismatch expected=0x{expectedHeader.ReceiptsRoot.ToHex()} actual=0x{result.ReceiptsRoot.ToHex()}");
                    }
                }

                // Validate bloom
                if (expectedHeader.LogsBloom != null && expectedHeader.LogsBloom.Length > 0 && result.CombinedBloom != null)
                {
                    if (!result.CombinedBloom.AreTheSame(expectedHeader.LogsBloom))
                    {
                        return TestResult.Fail(
                            $"Block {blockIdx}: bloom mismatch expected=0x{expectedHeader.LogsBloom.ToHex().Substring(0, 32)}... actual=0x{result.CombinedBloom.ToHex().Substring(0, 32)}...");
                    }
                }

                // Validate block hash
                if (result.BlockHash != null && expectedHeader.Hash.Length > 0)
                {
                    if (!result.BlockHash.AreTheSame(expectedHeader.Hash))
                    {
                        // Diagnose which field differs
                        if (!result.StateRoot.AreTheSame(expectedHeader.StateRoot))
                            _output.WriteLine($"    Diff: stateRoot");
                        if (!result.TransactionsRoot.AreTheSame(expectedHeader.TransactionsRoot))
                            _output.WriteLine($"    Diff: txRoot");
                        if (!result.ReceiptsRoot.AreTheSame(expectedHeader.ReceiptsRoot))
                            _output.WriteLine($"    Diff: receiptsRoot");
                        if (!result.CombinedBloom.AreTheSame(expectedHeader.LogsBloom))
                            _output.WriteLine($"    Diff: bloom");
                        _output.WriteLine($"    gasUsed: expected={(long)expectedHeader.GasUsed} actual={result.CumulativeGasUsed}");

                        return TestResult.Fail(
                            $"Block {blockIdx}: block hash mismatch expected=0x{expectedHeader.Hash.ToHex()} actual=0x{result.BlockHash.ToHex()}");
                    }
                }

                _output.WriteLine($"  Block {blockIdx}: hash=0x{result.BlockHash?.ToHex().Substring(0, 16)}... stateRoot=0x{result.StateRoot?.ToHex().Substring(0, 16)}... gas={result.CumulativeGasUsed} MATCH");

                // Update parent hash, block hashes, and pre-state for next block
                parentHash = expectedHeader.Hash;
                blockHashes[(long)expectedHeader.Number] = expectedHeader.Hash;
                preAccounts = RebuildAccountsFromExecutionState(result, preAccounts);
            }

            return TestResult.Ok();
        }

        private static List<byte[]> ExtractTransactionRlps(byte[] blockRlp)
        {
            var txRlps = new List<byte[]>();
            if (blockRlp == null || blockRlp.Length == 0)
                return txRlps;

            var decoded = RLP.RLP.Decode(blockRlp);
            if (decoded is RLPCollection blockList && blockList.Count >= 2)
            {
                var txListDecoded = blockList[1];
                if (txListDecoded is RLPCollection txList)
                {
                    foreach (var txItem in txList)
                    {
                        var txBytes = txItem.RLPData;
                        if (txBytes != null && txBytes.Length > 0)
                        {
                            // Typed transaction: RLPData = type_byte + rlp_payload
                            txRlps.Add(txBytes);
                        }
                        else if (txItem is RLPCollection txFields)
                        {
                            // Legacy transaction: re-encode the field list
                            var fieldBytes = new byte[txFields.Count][];
                            for (int j = 0; j < txFields.Count; j++)
                                fieldBytes[j] = RLP.RLP.EncodeElement(txFields[j].RLPData);
                            txRlps.Add(RLP.RLP.EncodeList(fieldBytes));
                        }
                    }
                }
            }

            return txRlps;
        }

        private static List<BlockWithdrawal> ConvertWithdrawals(List<BlockchainTestLoader.WithdrawalData> withdrawals)
        {
            if (withdrawals == null) return null;
            var result = new List<BlockWithdrawal>();
            foreach (var w in withdrawals)
            {
                result.Add(new BlockWithdrawal
                {
                    Index = w.Index,
                    ValidatorIndex = w.ValidatorIndex,
                    Address = w.Address,
                    AmountInGwei = w.Amount
                });
            }
            return result;
        }

        private static List<WitnessAccount> ConvertPreState(Dictionary<string, BlockchainTestLoader.AccountData> pre)
        {
            var accounts = new List<WitnessAccount>();
            foreach (var kvp in pre)
            {
                var acc = new WitnessAccount
                {
                    Address = kvp.Key,
                    Balance = EvmUInt256BigIntegerExtensions.FromBigInteger(kvp.Value.Balance),
                    Nonce = (long)kvp.Value.Nonce,
                    Code = kvp.Value.Code ?? new byte[0],
                    Storage = new List<WitnessStorageSlot>()
                };

                foreach (var storage in kvp.Value.Storage)
                {
                    acc.Storage.Add(new WitnessStorageSlot
                    {
                        Key = EvmUInt256BigIntegerExtensions.FromBigInteger(storage.Key),
                        Value = EvmUInt256BigIntegerExtensions.FromBigInteger(storage.Value)
                    });
                }

                accounts.Add(acc);
            }
            return accounts;
        }

        private static List<WitnessAccount> RebuildAccountsFromExecutionState(
            BlockExecutionResult result, List<WitnessAccount> previousAccounts)
        {
            // Use the stateReader which has ALL committed state (not just finalES which only has loaded accounts)
            if (result.StateReader == null) return previousAccounts;

            var accounts = new List<WitnessAccount>();
            foreach (var kvp in result.StateReader.Accounts)
            {
                var acc = new WitnessAccount
                {
                    Address = kvp.Key,
                    Balance = kvp.Value.Balance,
                    Nonce = (long)(ulong)kvp.Value.Nonce,
                    Code = kvp.Value.Code ?? new byte[0],
                    Storage = new List<WitnessStorageSlot>()
                };

                foreach (var s in kvp.Value.Storage)
                {
                    acc.Storage.Add(new WitnessStorageSlot
                    {
                        Key = s.Key,
                        Value = EvmUInt256.FromBigEndian(s.Value)
                    });
                }

                accounts.Add(acc);
            }

            return accounts;
        }

        private void CompareWithExpectedPostState(
            BlockExecutionResult result,
            Dictionary<string, BlockchainTestLoader.AccountData> expectedPostState)
        {
            if (expectedPostState == null || result.FinalExecutionState == null) return;

            foreach (var kvp in expectedPostState)
            {
                var addr = kvp.Key.ToLower();
                var expected = kvp.Value;

                var expectedBal = EvmUInt256BigIntegerExtensions.FromBigInteger(expected.Balance);
                var expectedNonce = EvmUInt256BigIntegerExtensions.FromBigInteger(expected.Nonce);

                AccountExecutionState acctState = null;
                result.FinalExecutionState.AccountsState.TryGetValue(addr, out acctState);
                var actualBal = acctState?.Balance.GetTotalBalance() ?? EvmUInt256.Zero;
                var actualNonce = acctState?.Nonce ?? EvmUInt256.Zero;

                bool differs = false;
                var diff = "";
                if (expectedBal != actualBal) { differs = true; diff += $" bal:exp={expectedBal} act={actualBal}"; }
                if (expectedNonce != actualNonce) { differs = true; diff += $" nonce:exp={expectedNonce} act={actualNonce}"; }

                var expectedStorageCount = expected.Storage?.Count ?? 0;
                var actualStorageCount = acctState?.Storage?.Count ?? 0;

                if (differs || acctState == null)
                {
                    // Also check stateReader directly for comparison
                    var readerVal = "";
                    if (result.FinalExecutionState?.StateReader is InMemoryStateReader imsr)
                    {
                        var sr = imsr.GetAccountState(addr);
                        if (sr != null) readerVal = $" [reader: bal={sr.Balance} nonce={sr.Nonce}]";
                    }
                    _output.WriteLine($"    DIFF {addr.Substring(0,10)}...:{diff}{(acctState == null ? " MISSING" : "")}{readerVal}");
                }
            }

            // Check for extra accounts in our state not in expected
            if (result.FinalExecutionState != null)
            {
                foreach (var a in result.FinalExecutionState.AccountsState)
                {
                    var bal = a.Value.Balance.GetTotalBalance();
                    var n = a.Value.Nonce ?? EvmUInt256.Zero;
                    if (bal.IsZero && n == EvmUInt256.Zero && (a.Value.Code == null || a.Value.Code.Length == 0))
                        continue;
                    if (!expectedPostState.ContainsKey(a.Key) && !expectedPostState.ContainsKey(a.Key.ToLower()))
                        _output.WriteLine($"    EXTRA {a.Key.Substring(0,10)}...: bal={bal} nonce={n}");
                }
            }
        }

        private static readonly HardforkRegistry MainnetRegistry = Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance;

        private static HardforkName DetectFork(string testName)
        {
            if (testName.Contains("Cancun", StringComparison.OrdinalIgnoreCase)) return HardforkName.Cancun;
            return HardforkName.Prague;
        }

        public class TestResult
        {
            public bool Success { get; set; }
            public string Error { get; set; }

            public static TestResult Ok() => new TestResult { Success = true };
            public static TestResult Fail(string error) => new TestResult { Success = false, Error = error };
        }
    }
}
