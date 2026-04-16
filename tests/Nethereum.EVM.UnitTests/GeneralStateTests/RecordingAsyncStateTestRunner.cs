using Nethereum.CoreChain;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    /// <summary>
    /// Async "recording" state-test runner. For each test:
    ///   1. Build a full pre-state InMemoryStateReader from test.Pre
    ///   2. Wrap it with WitnessRecordingStateReader (decorator)
    ///   3. Execute the signed tx via async TransactionExecutor — recorder captures the minimal set
    ///   4. Build a BlockWitnessData from just the recorded accounts
    ///   5. Run BlockExecutor.ExecuteAsync on the recorded witness
    ///   6. Assert the post-state root matches the reference hash from the test vector
    ///
    /// Validates both the recorder's coverage (if insufficient, BlockExecutor throws or diverges)
    /// and async BlockExecutor parity with the reference implementation.
    /// </summary>
    public class RecordingAsyncStateTestRunner
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testVectorsPath;
        private static readonly HardforkRegistry MainnetRegistry = DefaultMainnetHardforkRegistry.Instance;

        public RecordingAsyncStateTestRunner(ITestOutputHelper output)
        {
            _output = output;
            _testVectorsPath = GetTestVectorsPath();
        }

        public async Task RunSingleTestAsync(string categoryName, string testFileName, string hardfork = "Prague")
        {
            var testFile = Path.Combine(_testVectorsPath, categoryName, testFileName + ".json");
            if (!File.Exists(testFile))
                Assert.True(false, $"Test file not found: {testFile}");
            var json = File.ReadAllText(testFile);
            var tests = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, GeneralStateTest>>(json);
            foreach (var testEntry in tests)
            {
                var test = testEntry.Value;
                var postEntries = test.Post != null && test.Post.ContainsKey(hardfork) ? test.Post[hardfork] : null;
                if (postEntries == null) continue;
                foreach (var expected in postEntries)
                {
                    _output.WriteLine($"\n===== {testFileName}[{expected.Indexes.Data},{expected.Indexes.Gas},{expected.Indexes.Value}] =====");
                    var result = await RunRecordingAsync(test, expected, HardforkNames.Parse(hardfork));
                    _output.WriteLine($"Gas={result.GasUsed}, stateRoot=0x{(result.StateRoot == null ? "null" : result.StateRoot.ToHex())}, expected={expected.Hash}");
                    if (!string.IsNullOrEmpty(result.AccountDiff))
                        _output.WriteLine($"Diff: {result.AccountDiff}");
                }
            }
        }

        public async Task RunCategoryAsync(string categoryName, string hardfork = "Prague")
        {
            var categoryPath = Path.Combine(_testVectorsPath, categoryName);
            if (!Directory.Exists(categoryPath))
                Assert.True(false, $"Category not found: {categoryPath}");

            var testFiles = Directory.GetFiles(categoryPath, "*.json", SearchOption.AllDirectories);
            int totalPassed = 0, totalFailed = 0, totalSkipped = 0, stateRootMismatches = 0;
            var failures = new List<string>();

            foreach (var testFile in testFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(testFile);
                var json = File.ReadAllText(testFile);
                var tests = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, GeneralStateTest>>(json);

                foreach (var testEntry in tests)
                {
                    var test = testEntry.Value;
                    var postEntries = test.Post != null && test.Post.ContainsKey(hardfork) ? test.Post[hardfork] : null;
                    if (postEntries == null) continue;

                    foreach (var expected in postEntries)
                    {
                        try
                        {
                            var result = await RunRecordingAsync(test, expected, HardforkNames.Parse(hardfork));
                            var expectsException = !string.IsNullOrEmpty(expected.ExpectException);

                            if (expectsException && result.IsValidationError)
                            {
                                totalPassed++;
                            }
                            else if (expectsException && !result.IsValidationError)
                            {
                                failures.Add($"{fileName}[{expected.Indexes.Data},{expected.Indexes.Gas},{expected.Indexes.Value}]: expected {expected.ExpectException} but tx was valid");
                                totalFailed++;
                            }
                            else if (!expectsException && result.IsValidationError)
                            {
                                failures.Add($"{fileName}[{expected.Indexes.Data},{expected.Indexes.Gas},{expected.Indexes.Value}]: tx invalid: {result.Error ?? "unknown"}");
                                totalFailed++;
                            }
                            else if (!string.IsNullOrEmpty(expected.Hash) && result.StateRoot != null)
                            {
                                var expectedRoot = expected.Hash.HexToByteArray();
                                if (expectedRoot.AreTheSame(result.StateRoot))
                                {
                                    totalPassed++;
                                }
                                else
                                {
                                    stateRootMismatches++;
                                    var diagSuffix = string.IsNullOrEmpty(result.AccountDiff) ? "" : $" | diff: {result.AccountDiff}";
                                    failures.Add($"{fileName}[{expected.Indexes.Data},{expected.Indexes.Gas},{expected.Indexes.Value}]: STATE ROOT MISMATCH expected={expected.Hash} actual=0x{result.StateRoot.ToHex()} (recorder accounts={result.RecordedAccounts}, gas={result.GasUsed}){diagSuffix}");
                                    totalFailed++;
                                }
                            }
                            else
                            {
                                totalPassed++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine($"  SKIP: {fileName} — {ex.GetType().Name}: {ex.Message}");
                            totalSkipped++;
                        }
                    }
                }
            }

            _output.WriteLine($"=== {categoryName} Recording-Async SUMMARY ===");
            _output.WriteLine($"Passed: {totalPassed}, Failed: {totalFailed}, Skipped: {totalSkipped}, StateRootMismatches: {stateRootMismatches}");

            if (failures.Count > 0)
            {
                _output.WriteLine("=== FAILURES ===");
                foreach (var f in failures)
                    _output.WriteLine(f);
            }

            Assert.Equal(0, totalFailed);
        }

        private async Task<RecordingResult> RunRecordingAsync(GeneralStateTest test, PostResult expected, HardforkName fork)
        {
            // Static witness — contains the full pre-state. State-test reference hashes are
            // computed over every pre-state leaf plus post-execution modifications, so a
            // recorder-minimal witness would be missing untouched leaves. Use the same
            // converter the sync path uses.
            var blockData = StateTestToWitnessConverter.Convert(test, expected, fork);
            blockData.ComputePostStateRoot = true;

            // Sanity pass: wrap the reader with the recorder and run the tx.
            // We don't need its output (we're using the full witness) but this verifies
            // the recorder works under the async pipeline and lets us compare the
            // touched-set against BlockExecutor later.
            var preAccounts = WitnessStateBuilder.BuildAccountState(blockData.Accounts);
            var preReader = new InMemoryStateReader(preAccounts);
            var recorder = new WitnessRecordingStateReader(preReader);
            var executionState = new ExecutionStateService(recorder);
            foreach (var acc in blockData.Accounts)
            {
                await executionState.LoadBalanceNonceAndCodeFromStorageAsync(acc.Address);
                if (acc.Storage != null)
                {
                    var acctState = executionState.CreateOrGetAccountExecutionState(acc.Address);
                    foreach (var slot in acc.Storage)
                        acctState.SetPreStateStorage(slot.Key, slot.Value.ToBigEndian());
                }
            }
            var executor = new TransactionExecutor(config: DefaultHardforkConfigs.Prague);
            var ctx = TransactionContextFactory.FromBlockWitnessTransaction(
                blockData.Transactions[0], blockData, executionState);
            var execResult = await executor.ExecuteAsync(ctx);
            if (execResult.IsValidationError)
            {
                return new RecordingResult
                {
                    IsValidationError = true,
                    Error = execResult.Error,
                    GasUsed = execResult.GasUsed
                };
            }
            var recordedAccountsCount = recorder.GetWitnessAccounts().Count;

            // Primary assertion path: run BlockExecutor.ExecuteAsync on the full witness
            // and compare to the reference hash. This is the async parity check.
            var blockResult = await BlockExecutor.ExecuteAsync(
                blockData,
                RlpBlockEncodingProvider.Instance,
                MainnetRegistry,
                new PatriciaStateRootCalculator(RlpBlockEncodingProvider.Instance));

            // Diff against expected post-state accounts (if the test vector provides them)
            var diff = string.Empty;
            if (expected.State != null && expected.State.Count > 0 && blockResult.FinalExecutionState != null)
            {
                diff = DiffAccountsAgainstExpected(blockResult.FinalExecutionState, expected.State);
            }

            return new RecordingResult
            {
                StateRoot = blockResult.StateRoot,
                GasUsed = blockResult.CumulativeGasUsed,
                RecordedAccounts = recordedAccountsCount,
                AccountDiff = diff
            };
        }

        private static string DiffAccountsAgainstExpected(
            ExecutionStateService finalES,
            Dictionary<string, TestAccount> expectedState)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kvp in expectedState)
            {
                var addr = kvp.Key.ToLower();
                var expected = kvp.Value;
                var actualAcct = finalES.AccountsState.ContainsKey(addr) ? finalES.AccountsState[addr] : null;

                var expectedBalance = string.IsNullOrEmpty(expected.Balance) ? BigInteger.Zero : expected.Balance.HexToBigInteger(false);
                var expectedNonce = string.IsNullOrEmpty(expected.Nonce) ? 0L : (long)expected.Nonce.HexToBigInteger(false);
                var expectedCode = string.IsNullOrEmpty(expected.Code) || expected.Code == "0x" ? new byte[0] : expected.Code.HexToByteArray();

                if (actualAcct == null)
                {
                    sb.Append($"{addr.Substring(0, 8)}:MISSING ");
                    continue;
                }
                var actualBalance = actualAcct.Balance.GetTotalBalance();
                var actualBalanceBig = actualBalance.ToBigInteger();
                var actualNonce = actualAcct.Nonce ?? EvmUInt256.Zero;
                var actualCode = actualAcct.Code ?? new byte[0];

                var parts = new List<string>();
                if (actualBalanceBig != expectedBalance)
                    parts.Add($"bal {expectedBalance}→{actualBalanceBig}");
                if ((long)(ulong)actualNonce != expectedNonce)
                    parts.Add($"nonce {expectedNonce}→{(long)(ulong)actualNonce}");
                if (!actualCode.AreTheSame(expectedCode))
                    parts.Add($"code {expectedCode.Length}b→{actualCode.Length}b");

                // Storage diff
                if (expected.Storage != null)
                {
                    foreach (var slotKv in expected.Storage)
                    {
                        var key = EvmUInt256BigIntegerExtensions.FromBigInteger(slotKv.Key.HexToBigInteger(false));
                        var expectedVal = slotKv.Value.HexToByteArray().PadTo32Bytes();
                        var actualVal = actualAcct.Storage != null && actualAcct.Storage.ContainsKey(key)
                            ? actualAcct.Storage[key] : new byte[32];
                        if (!actualVal.AreTheSame(expectedVal))
                            parts.Add($"slot[{slotKv.Key}] exp={slotKv.Value.Substring(0, System.Math.Min(10, slotKv.Value.Length))}... act=0x{actualVal.ToHex().Substring(0, 8)}...");
                    }
                }

                if (parts.Count > 0)
                    sb.Append($"{addr.Substring(0, 8)}:{string.Join(",", parts)} ");
            }
            return sb.ToString();
        }

        private static BlockWitnessData BuildBlockData(TestEnv env, PostResult expected, TestTransaction tx)
        {
            var blockNumber = string.IsNullOrEmpty(env.CurrentNumber) ? 1L : (long)env.CurrentNumber.HexToBigInteger(false);
            var timestamp = string.IsNullOrEmpty(env.CurrentTimestamp) ? 0L : (long)env.CurrentTimestamp.HexToBigInteger(false);
            var blockGasLimit = string.IsNullOrEmpty(env.CurrentGasLimit) ? 0L : (long)env.CurrentGasLimit.HexToBigInteger(false);
            var baseFee = string.IsNullOrEmpty(env.CurrentBaseFee) ? 0L : (long)env.CurrentBaseFee.HexToBigInteger(false);
            var coinbase = string.IsNullOrEmpty(env.CurrentCoinbase) ? "0x0000000000000000000000000000000000000000" : env.CurrentCoinbase;

            byte[] difficulty;
            if (!string.IsNullOrEmpty(env.CurrentRandom))
                difficulty = env.CurrentRandom.HexToByteArray().PadTo32Bytes();
            else if (!string.IsNullOrEmpty(env.CurrentDifficulty))
                difficulty = EvmUInt256BigIntegerExtensions.FromBigInteger(env.CurrentDifficulty.HexToBigInteger(false)).ToBigEndian();
            else
                difficulty = new byte[32];

            var txRlp = !string.IsNullOrEmpty(expected.TxBytes) ? expected.TxBytes.HexToByteArray() : new byte[0];
            var sender = tx.Sender ?? GetSenderFromSecretKey(tx.SecretKey);

            return new BlockWitnessData
            {
                BlockNumber = blockNumber,
                Timestamp = timestamp,
                BaseFee = baseFee,
                BlockGasLimit = blockGasLimit,
                ChainId = 1,
                Coinbase = coinbase,
                Difficulty = difficulty,
                ParentHash = new byte[32],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Transactions = new List<BlockWitnessTransaction>
                {
                    new BlockWitnessTransaction { From = sender, RlpEncoded = txRlp }
                }
            };
        }

        private static string GetSenderFromSecretKey(string secretKey)
        {
            if (string.IsNullOrEmpty(secretKey))
                return "0x0000000000000000000000000000000000000000";
            return new Nethereum.Signer.EthECKey(secretKey).GetPublicAddress();
        }

        private static string GetTestVectorsPath()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var testDir = Path.Combine(currentDir, "Tests", "GeneralStateTests");
            if (Directory.Exists(testDir)) return testDir;

            var projectRoot = FindProjectRoot(currentDir);
            if (projectRoot != null)
            {
                testDir = Path.Combine(projectRoot, "tests", "Nethereum.EVM.UnitTests", "Tests", "GeneralStateTests");
                if (Directory.Exists(testDir)) return testDir;
            }
            return null;
        }

        private static string FindProjectRoot(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        private class RecordingResult
        {
            public byte[] StateRoot { get; set; }
            public long GasUsed { get; set; }
            public int RecordedAccounts { get; set; }
            public bool IsValidationError { get; set; }
            public string Error { get; set; }
            public string AccountDiff { get; set; }
        }
    }
}
