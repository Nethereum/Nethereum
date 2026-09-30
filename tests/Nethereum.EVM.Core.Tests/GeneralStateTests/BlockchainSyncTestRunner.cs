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
using Nethereum.Signer;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;
using Nethereum.Merkle.Patricia.Nodes;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class BlockchainSyncTestRunner
    {
        private readonly ITestOutputHelper _output;

        public BlockchainSyncTestRunner(ITestOutputHelper output)
        {
            _output = output;
        }

        private static List<string> RecoverAuthorities(byte[] rlpEncoded)
        {
            try
            {
                return TransactionFactory.CreateTransaction(rlpEncoded).RecoverAuthorities();
            }
            catch (Exception ex) when (!EvmHostException.IsHostOrSystemFault(ex))
            {
                return null;
            }
        }

        public void RunCategory(string categoryPath, string targetNetwork = "Cancun")
        {
            if (!Directory.Exists(categoryPath))
            {
                _output.WriteLine($"Category not found: {categoryPath}");
                Assert.Fail($"Category not found: {categoryPath}");
                return;
            }

            HardforkName? targetFork = string.IsNullOrEmpty(targetNetwork)
                ? (HardforkName?)null
                : HardforkNames.Parse(targetNetwork);

            var testFiles = Directory.GetFiles(categoryPath, "*.json", SearchOption.AllDirectories);
            int totalPassed = 0, totalFailed = 0, totalSkipped = 0;
            var failures = new List<string>();

            foreach (var testFile in testFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(testFile);
                var tests = BlockchainTestLoader.LoadFromFile(testFile);

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
                        failures.Add($"{test.Name}: {ex.Message}");
                        totalFailed++;
                        continue;
                    }

                    if (targetFork.HasValue && fork != targetFork.Value)
                    {
                        totalSkipped++;
                        continue;
                    }

                    try
                    {
                        var result = ExecuteTest(test, fork);
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
                        _output.WriteLine($"  FAIL: {test.Name} — {ex.GetType().Name}: {ex.Message}");
                        failures.Add($"{test.Name}: {ex.GetType().Name}: {ex.Message}");
                        totalFailed++;
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

            if (totalPassed + totalFailed == 0)
            {
                Assert.Fail($"No test executed for category '{categoryPath}' targeting network '{targetNetwork}': no test matched.");
            }

            Assert.Equal(0, totalFailed);
        }

        public TestResult ExecuteTest(BlockchainTestLoader.BlockchainTest test, HardforkName fork)
        {
            var encoding = RlpBlockEncodingProvider.Instance;
            byte[] parentHash = test.GenesisBlockHeader.Hash;

            var blockHashes = new Dictionary<long, byte[]>();
            if (test.GenesisBlockHeader.Hash != null && test.GenesisBlockHeader.Hash.Length > 0)
                blockHashes[(long)test.GenesisBlockHeader.Number] = test.GenesisBlockHeader.Hash;

            var preAccounts = ConvertPreState(test.Pre);

            if (test.GenesisBlockHeader.StateRoot != null && test.GenesisBlockHeader.StateRoot.Length > 0)
            {
                var genesisReader = new InMemoryStateReader(WitnessStateBuilder.BuildAccountState(preAccounts));
                var genesisES = new ExecutionStateService(genesisReader);
                WitnessStateBuilder.LoadAllAccountsAndStorage(genesisES, genesisReader, preAccounts);
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

                bool expectingRejection = !string.IsNullOrEmpty(blockData.ExpectException);

                BlockchainTestLoader.BlockHeader expectedHeader;
                List<BlockchainTestLoader.WithdrawalData> withdrawalsForBlock;
                if (expectingRejection)
                {
                    try
                    {
                        expectedHeader = DecodeHeaderFromBlockRlp(blockData.Rlp);
                        withdrawalsForBlock = DecodeWithdrawalsFromBlockRlp(blockData.Rlp);
                    }
                    catch (Exception ex)
                    {
                        if (!ExpectedRejectionMatcher.Satisfies(blockData.ExpectException, ex, out var decodeDetail))
                        {
                            return TestResult.Fail(
                                $"Block {blockIdx}: expected exception=\"{blockData.ExpectException}\" but the block RLP did not decode — {decodeDetail}");
                        }

                        _output.WriteLine($"  Block {blockIdx}: correctly rejected — undecodable; {decodeDetail}");
                        continue;
                    }
                }
                else
                {
                    expectedHeader = blockData.BlockHeader;
                    withdrawalsForBlock = blockData.Withdrawals;
                }

                BlockExecutionResult executedResult = null;

                TestResult RunBlock()
                {
                    var txRlps = ExtractTransactionRlps(blockData.Rlp);

                    if (fork >= HardforkName.Prague)
                        Nethereum.EVM.Witness.HistoryContractHelpers.PopulateFromBlockHashes(preAccounts, blockHashes, (long)expectedHeader.Number);

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
                        Withdrawals = ConvertWithdrawals(withdrawalsForBlock),
                        BlobGasUsed = expectedHeader.BlobGasUsed,
                        ExcessBlobGas = expectedHeader.ExcessBlobGas,
                        ParentBeaconBlockRoot = expectedHeader.ParentBeaconBlockRoot,
                        RequestsHash = expectedHeader.RequestsHash,
                        SlotNumber = expectedHeader.SlotNumber.HasValue ? (ulong?)expectedHeader.SlotNumber.Value : null,
                        ProduceBlockCommitments = true,
                        ComputePostStateRoot = true,
                        Transactions = new List<BlockWitnessTransaction>(),
                        DeclaredBlockAccessList = blockData.BlockAccessList,
                        Accounts = preAccounts,
                        Features = new BlockFeatureConfig { Fork = fork }
                    };

                    for (int i = 0; i < txRlps.Count; i++)
                    {
                        var sender = i < blockData.Transactions.Count
                            ? blockData.Transactions[i].Sender
                            : "";

                        block.Transactions.Add(new BlockWitnessTransaction
                        {
                            From = string.IsNullOrEmpty(sender) ? "" : sender,
                            RlpEncoded = txRlps[i],
                            AuthorisationAuthorities = RecoverAuthorities(txRlps[i])
                        });
                    }

                    if (block.ParentBeaconBlockRoot != null && block.ParentBeaconBlockRoot.Length > 0)
                    {
                        _output.WriteLine($"    Beacon root call: parentBeaconRoot=0x{block.ParentBeaconBlockRoot.ToHex().Substring(0, 16)}... timestamp={block.Timestamp}");
                    }

                    var result = BlockExecutor.Execute(
                        block,
                        encoding,
                        FixtureRegistry,
                        new PatriciaStateRootCalculator(encoding),
                        new PatriciaBlockRootCalculator());
                    executedResult = result;

                    _output.WriteLine($"  Block {blockIdx}: txCount={block.Transactions.Count} gasUsed={result.CumulativeGasUsed} expectedGas={(long)expectedHeader.GasUsed}");
                    for (int t = 0; t < result.TxResults.Count; t++)
                    {
                        var txr = result.TxResults[t];
                        var txType = block.Transactions[t].RlpEncoded?.Length > 0 ? block.Transactions[t].RlpEncoded[0] : (byte)0;
                        _output.WriteLine($"    tx[{t}]: type=0x{txType:x2} success={txr.Success} gas={txr.GasUsed}{(txr.Error != null ? " err=" + txr.Error : "")}");
                    }
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

                    if (result.BlockAccessListMalformed)
                    {
                        return TestResult.Fail(BlockValidityCheck.BlockAccessListMalformed,
                            $"Block {blockIdx}: the declared block access list is malformed — "
                            + result.DeclaredBlockAccessListCheck);
                    }

                    if (result.BlockAccessListGasLimitExceeded)
                    {
                        return TestResult.Fail(BlockValidityCheck.BlockAccessListGasLimit,
                            $"Block {blockIdx}: block access list holds {BlockAccessListSizeRule.CountItems(result.BlockAccessList)} items, "
                            + $"more than gasLimit {block.BlockGasLimit} allows");
                    }

                    if (result.StateRoot != null && expectedHeader.StateRoot.Length > 0)
                    {
                        if (!result.StateRoot.AreTheSame(expectedHeader.StateRoot))
                        {
                            CompareWithExpectedPostState(result, test.PostState);
                            return TestResult.Fail(BlockValidityCheck.StateRoot,
                                $"Block {blockIdx}: state root mismatch expected=0x{expectedHeader.StateRoot.ToHex()} actual=0x{result.StateRoot.ToHex()}");
                        }
                    }

                    if (result.TransactionsRoot != null && expectedHeader.TransactionsRoot.Length > 0)
                    {
                        if (!result.TransactionsRoot.AreTheSame(expectedHeader.TransactionsRoot))
                        {
                            return TestResult.Fail(
                                $"Block {blockIdx}: tx root mismatch expected=0x{expectedHeader.TransactionsRoot.ToHex()} actual=0x{result.TransactionsRoot.ToHex()}");
                        }
                    }

                    if (result.ReceiptsRoot != null && expectedHeader.ReceiptsRoot.Length > 0)
                    {
                        if (!result.ReceiptsRoot.AreTheSame(expectedHeader.ReceiptsRoot))
                        {
                            return TestResult.Fail(BlockValidityCheck.ReceiptsRoot,
                                $"Block {blockIdx}: receipts root mismatch expected=0x{expectedHeader.ReceiptsRoot.ToHex()} actual=0x{result.ReceiptsRoot.ToHex()}");
                        }
                    }

                    if (expectedHeader.LogsBloom != null && expectedHeader.LogsBloom.Length > 0 && result.CombinedBloom != null)
                    {
                        if (!result.CombinedBloom.AreTheSame(expectedHeader.LogsBloom))
                        {
                            return TestResult.Fail(BlockValidityCheck.LogsBloom,
                                $"Block {blockIdx}: bloom mismatch expected=0x{expectedHeader.LogsBloom.ToHex().Substring(0, 32)}... actual=0x{result.CombinedBloom.ToHex().Substring(0, 32)}...");
                        }
                    }

                    if (result.ProducedHeader != null &&
                        HeaderBytesDiffer(result.ProducedHeader.BlockAccessListHash, expectedHeader.BlockAccessListHash))
                    {
                        ReportAccessListDiffs(result.BlockAccessList, blockData.BlockAccessList);
                        return TestResult.Fail(BlockValidityCheck.BlockAccessListHash,
                            $"Block {blockIdx}: block access list hash mismatch expected={HashOrNone(expectedHeader.BlockAccessListHash)} actual={HashOrNone(result.ProducedHeader.BlockAccessListHash)}");
                    }

                    if (result.BlockHash != null && expectedHeader.Hash.Length > 0)
                    {
                        if (!result.BlockHash.AreTheSame(expectedHeader.Hash))
                        {
                            if (!result.StateRoot.AreTheSame(expectedHeader.StateRoot))
                                _output.WriteLine($"    Diff: stateRoot");
                            if (!result.TransactionsRoot.AreTheSame(expectedHeader.TransactionsRoot))
                                _output.WriteLine($"    Diff: txRoot");
                            if (!result.ReceiptsRoot.AreTheSame(expectedHeader.ReceiptsRoot))
                                _output.WriteLine($"    Diff: receiptsRoot");
                            if (!result.CombinedBloom.AreTheSame(expectedHeader.LogsBloom))
                                _output.WriteLine($"    Diff: bloom");
                            if (result.HeaderGasUsed != (long)expectedHeader.GasUsed)
                                _output.WriteLine($"    Diff: gasUsed expected={(long)expectedHeader.GasUsed} actual={result.HeaderGasUsed}");

                            ReportHeaderFieldDiffs(result.ProducedHeader, expectedHeader);

                            _output.WriteLine($"    (receipts cumulativeGasUsed={result.CumulativeGasUsed} — not the header field)");

                            return TestResult.Fail(
                                $"Block {blockIdx}: block hash mismatch expected=0x{expectedHeader.Hash.ToHex()} actual=0x{result.BlockHash.ToHex()}");
                        }
                    }

                    _output.WriteLine($"  Block {blockIdx}: hash=0x{result.BlockHash?.ToHex().Substring(0, 16)}... stateRoot=0x{result.StateRoot?.ToHex().Substring(0, 16)}... gas={result.CumulativeGasUsed} MATCH");

                    return TestResult.Ok();
                }

                TestResult blockResult;
                Exception executionFault = null;
                try
                {
                    blockResult = RunBlock();
                }
                catch (Exception ex) when (expectingRejection)
                {
                    blockResult = TestResult.Fail($"execution threw {ex.GetType().Name}");
                    executionFault = ex;
                }

                if (expectingRejection)
                {
                    if (blockResult.Success)
                    {
                        return TestResult.Fail(
                            $"Block {blockIdx} ACCEPTED a block that must be rejected (expected exception=\"{blockData.ExpectException}\")");
                    }

                    var observed = ObservedRejection.FromImport(
                        blockResult.FailedCheck.HasValue
                            ? new[] { blockResult.FailedCheck.Value }
                            : null,
                        FirstTransactionError(executedResult),
                        executionFault);

                    if (!ExpectedRejectionMatcher.Satisfies(blockData.ExpectException, observed, out var rejectionDetail))
                    {
                        return TestResult.Fail(
                            $"Block {blockIdx}: expected exception=\"{blockData.ExpectException}\" but {rejectionDetail} (engine reported: {blockResult.Error})");
                    }

                    _output.WriteLine($"  Block {blockIdx}: correctly rejected — {rejectionDetail}");
                    continue;
                }

                if (!blockResult.Success)
                    return TestResult.Fail(blockResult.Error);

                parentHash = expectedHeader.Hash;
                blockHashes[(long)expectedHeader.Number] = expectedHeader.Hash;
                preAccounts = RebuildAccountsFromExecutionState(executedResult, preAccounts);
            }

            return TestResult.Ok();
        }

        private static BlockchainTestLoader.BlockHeader DecodeHeaderFromBlockRlp(byte[] blockRlp)
        {
            var decoded = RLP.RLP.Decode(blockRlp);
            if (!(decoded is RLPCollection blockList) || blockList.Count < 1)
                throw new InvalidOperationException("Block RLP does not contain a header element.");

            var headerRlp = blockList[0].RLPData;
            var header = BlockHeaderEncoder.Current.Decode(headerRlp);

            return new BlockchainTestLoader.BlockHeader
            {
                Hash = new Sha3Keccack().CalculateHash(headerRlp),
                ParentHash = header.ParentHash,
                StateRoot = header.StateRoot,
                TransactionsRoot = header.TransactionsHash,
                ReceiptsRoot = header.ReceiptHash,
                UncleHash = header.UnclesHash,
                Coinbase = header.Coinbase.HexToByteArray(),
                LogsBloom = header.LogsBloom,
                Difficulty = header.Difficulty.ToBigInteger(),
                Number = header.BlockNumber.ToBigInteger(),
                GasLimit = header.GasLimit,
                GasUsed = header.GasUsed,
                Timestamp = header.Timestamp,
                ExtraData = header.ExtraData,
                MixHash = header.MixHash,
                Nonce = header.Nonce,
                BaseFee = header.BaseFee.HasValue ? header.BaseFee.Value.ToBigInteger() : (BigInteger?)null,
                WithdrawalsRoot = header.WithdrawalsRoot,
                BlobGasUsed = header.BlobGasUsed,
                ExcessBlobGas = header.ExcessBlobGas,
                ParentBeaconBlockRoot = header.ParentBeaconBlockRoot,
                RequestsHash = header.RequestsHash,
                BlockAccessListHash = header.BlockAccessListHash,
                SlotNumber = header.SlotNumber.HasValue ? (BigInteger?)header.SlotNumber.Value : null
            };
        }

        private static List<BlockchainTestLoader.WithdrawalData> DecodeWithdrawalsFromBlockRlp(byte[] blockRlp)
        {
            var decoded = RLP.RLP.Decode(blockRlp);
            if (!(decoded is RLPCollection blockList) || blockList.Count < 4)
                return null;

            var withdrawalList = (RLPCollection)blockList[3];
            var result = new List<BlockchainTestLoader.WithdrawalData>();
            foreach (var wRlp in withdrawalList)
            {
                var w = WithdrawalEncoder.Current.Decode(wRlp.RLPData);
                result.Add(new BlockchainTestLoader.WithdrawalData
                {
                    Index = w.Index,
                    ValidatorIndex = w.ValidatorIndex,
                    Address = "0x" + w.Address.ToHex(),
                    Amount = w.AmountInGwei
                });
            }
            return result;
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
                            txRlps.Add(txBytes);
                        }
                        else if (txItem is RLPCollection txFields)
                        {
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
                    Nonce = (ulong)kvp.Value.Nonce,
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
            if (result.StateReader == null) return previousAccounts;

            var accounts = new List<WitnessAccount>();
            foreach (var kvp in result.StateReader.Accounts)
            {
                var acc = new WitnessAccount
                {
                    Address = kvp.Key,
                    Balance = kvp.Value.Balance,
                    Nonce = (ulong)kvp.Value.Nonce,
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
                result.FinalExecutionState.AccountsState.TryGetValue(Nethereum.Util.EvmAddress.FromHex(addr), out acctState);
                var actualBal = acctState?.Balance.GetTotalBalance() ?? EvmUInt256.Zero;
                var actualNonce = acctState?.Nonce ?? EvmUInt256.Zero;

                bool differs = false;
                var diff = "";
                if (expectedBal != actualBal) { differs = true; diff += $" bal:exp={expectedBal} act={actualBal}"; }
                if (expectedNonce != actualNonce) { differs = true; diff += $" nonce:exp={expectedNonce} act={actualNonce}"; }

                var expectedStorageCount = expected.Storage?.Count ?? 0;
                var actualStorageCount = acctState?.Storage?.Count ?? 0;
                if (expectedStorageCount != actualStorageCount) { differs = true; diff += $" storageCount:exp={expectedStorageCount} act={actualStorageCount}"; }

                if (differs || acctState == null)
                {
                    var readerVal = "";
                    if (result.FinalExecutionState?.StateReader is InMemoryStateReader imsr)
                    {
                        var sr = imsr.GetAccountState(addr);
                        if (sr != null) readerVal = $" [reader: bal={sr.Balance} nonce={sr.Nonce}]";
                    }
                    _output.WriteLine($"    DIFF {addr.Substring(0,10)}...:{diff}{(acctState == null ? " MISSING" : "")}{readerVal}");
                }
            }

            if (result.FinalExecutionState != null)
            {
                foreach (var a in result.FinalExecutionState.AccountsState)
                {
                    var bal = a.Value.Balance.GetTotalBalance();
                    var n = a.Value.Nonce ?? EvmUInt256.Zero;
                    if (bal.IsZero && n == EvmUInt256.Zero && (a.Value.Code == null || a.Value.Code.Length == 0))
                        continue;
                    var aKey = a.Key.ToHexLower();
                    if (!expectedPostState.ContainsKey(aKey) && !expectedPostState.ContainsKey(aKey.ToLower()))
                        _output.WriteLine($"    EXTRA {aKey.Substring(0,10)}...: bal={bal} nonce={n}");
                }
            }
        }

        public static readonly HardforkRegistry FixtureRegistry =
            Nethereum.EVM.Precompiles.Bls.Bls12381AwareMainnetHardforkRegistry.Build(
                Nethereum.EVM.Precompiles.Kzg.KzgAwareMainnetHardforkRegistry.Instance,
                new Nethereum.Signer.Bls.Herumi.Bls12381Operations());

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

        private static TransactionError FirstTransactionError(BlockExecutionResult executed)
        {
            if (executed?.TxResults == null) return TransactionError.None;
            foreach (var txResult in executed.TxResults)
                if (txResult.ErrorCode != TransactionError.None) return txResult.ErrorCode;
            return TransactionError.None;
        }

        public class TestResult
        {
            public bool Success { get; set; }
            public string Error { get; set; }

            public BlockValidityCheck? FailedCheck { get; set; }

            public static TestResult Ok() => new TestResult { Success = true };
            public static TestResult Fail(string error) => new TestResult { Success = false, Error = error };
            public static TestResult Fail(BlockValidityCheck check, string error) =>
                new TestResult { Success = false, Error = error, FailedCheck = check };
        }

        private static string Shorten(byte[] value)
        {
            var hex = value.ToHex();
            return hex.Length <= 16 ? hex : hex.Substring(0, 16) + "...";
        }

        private static string HashOrNone(byte[] value) =>
            value == null || value.Length == 0 ? "<none>" : "0x" + value.ToHex();

        private static bool HeaderBytesDiffer(byte[] produced, byte[] declared)
        {
            if ((produced == null || produced.Length == 0) && (declared == null || declared.Length == 0))
                return false;
            return produced == null || declared == null || !produced.AreTheSame(declared);
        }

        private void ReportAccessListDiffs(
            List<Nethereum.Model.AccountChanges> produced,
            List<Nethereum.Model.AccountChanges> expected)
        {
            var differences = BlockAccessListDiff.Describe(produced, expected);
            if (differences.Count == 0)
            {
                _output.WriteLine("    BAL: identical to the fixture's — the difference is in the ENCODING, not the collection.");
                return;
            }

            foreach (var line in differences)
                _output.WriteLine("    BAL " + line);
        }

        private void ReportHeaderFieldDiffs(
            Nethereum.Model.BlockHeader produced,
            BlockchainTestLoader.BlockHeader expected)
        {
            if (produced == null)
            {
                _output.WriteLine("    (header fields not compared — the engine did not expose the header it hashed)");
                return;
            }

            var differences = 0;
            void Bytes(string name, byte[] a, byte[] b)
            {
                if (!HeaderBytesDiffer(a, b)) return;
                _output.WriteLine($"    Diff: {name} expected=0x{(b == null ? "<null>" : Shorten(b))} actual=0x{(a == null ? "<null>" : Shorten(a))}");
                differences++;
            }
            void Number(string name, System.Numerics.BigInteger? a, System.Numerics.BigInteger? b)
            {
                if (a != b)
                {
                    _output.WriteLine($"    Diff: {name} expected={(b?.ToString() ?? "<null>")} actual={(a?.ToString() ?? "<null>")}");
                    differences++;
                }
            }

            Bytes("parentHash", produced.ParentHash, expected.ParentHash);
            Bytes("unclesHash", produced.UnclesHash, expected.UncleHash);
            Bytes("coinbase", produced.Coinbase?.HexToByteArray(), expected.Coinbase);
            Bytes("extraData", produced.ExtraData, expected.ExtraData);
            Bytes("mixHash", produced.MixHash, expected.MixHash);
            Bytes("nonce", produced.Nonce, expected.Nonce);
            Bytes("withdrawalsRoot", produced.WithdrawalsRoot, expected.WithdrawalsRoot);
            Bytes("parentBeaconBlockRoot", produced.ParentBeaconBlockRoot, expected.ParentBeaconBlockRoot);
            Bytes("requestsHash", produced.RequestsHash, expected.RequestsHash);
            Bytes("blockAccessListHash", produced.BlockAccessListHash, expected.BlockAccessListHash);

            Number("difficulty", produced.Difficulty.ToBigInteger(), expected.Difficulty);
            Number("number", produced.BlockNumber, expected.Number);
            Number("gasLimit", produced.GasLimit, expected.GasLimit);
            Number("timestamp", produced.Timestamp, expected.Timestamp);
            Number("baseFee", produced.BaseFee, expected.BaseFee);
            Number("blobGasUsed", produced.BlobGasUsed, expected.BlobGasUsed);
            Number("excessBlobGas", produced.ExcessBlobGas, expected.ExcessBlobGas);
            Number("slotNumber", produced.SlotNumber, expected.SlotNumber);

            if (differences == 0)
                _output.WriteLine("    All 23 header fields match — the difference is in the ENCODING, not a field value.");
        }
    }
}
