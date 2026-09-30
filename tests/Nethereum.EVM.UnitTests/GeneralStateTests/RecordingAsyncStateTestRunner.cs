using Nethereum.CoreChain;
using Nethereum.CoreChain.IntegrationTests.BlockchainTests;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Precompiles.Bls;
using Nethereum.EVM.Precompiles.Kzg;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
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
    public class RecordingAsyncStateTestRunner
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testVectorsPath;
        private static readonly Nethereum.Signer.Bls.Herumi.Bls12381Operations Bls =
            new Nethereum.Signer.Bls.Herumi.Bls12381Operations();

        private static readonly HardforkRegistry MainnetRegistry =
            Bls12381AwareMainnetHardforkRegistry.Build(KzgAwareMainnetHardforkRegistry.Instance, Bls);

        private static HardforkConfig WithPluginBackends(HardforkConfig config, HardforkName fork)
        {
            if (fork >= HardforkName.Cancun) config = config.WithKzgBackend();
            if (fork >= HardforkName.Prague) config = config.WithBlsBackend(Bls);
            return config;
        }

        private static HardforkConfig SelectConfig(HardforkName fork)
        {
            switch (fork)
            {
                case HardforkName.Frontier: return DefaultHardforkConfigs.Frontier;
                case HardforkName.Homestead: return DefaultHardforkConfigs.Homestead;
                case HardforkName.TangerineWhistle: return DefaultHardforkConfigs.TangerineWhistle;
                case HardforkName.SpuriousDragon: return DefaultHardforkConfigs.SpuriousDragon;
                case HardforkName.Byzantium: return DefaultHardforkConfigs.Byzantium;
                case HardforkName.Constantinople: return DefaultHardforkConfigs.Constantinople;
                case HardforkName.Petersburg: return DefaultHardforkConfigs.Petersburg;
                case HardforkName.Istanbul: return DefaultHardforkConfigs.Istanbul;
                case HardforkName.Berlin: return DefaultHardforkConfigs.Berlin;
                case HardforkName.London: return DefaultHardforkConfigs.London;
                case HardforkName.Paris: return DefaultHardforkConfigs.Paris;
                case HardforkName.Shanghai: return DefaultHardforkConfigs.Shanghai;
                case HardforkName.Cancun: return DefaultHardforkConfigs.Cancun;
                case HardforkName.Prague: return DefaultHardforkConfigs.Prague;
                case HardforkName.Osaka: return DefaultHardforkConfigs.Osaka;
                default: return DefaultHardforkConfigs.Default;
            }
        }

        public RecordingAsyncStateTestRunner(ITestOutputHelper output)
            : this(output, GetTestVectorsPath())
        {
        }

        internal RecordingAsyncStateTestRunner(ITestOutputHelper output, string testVectorsPath)
        {
            _output = output;
            _testVectorsPath = testVectorsPath;
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
                    _output.WriteLine($"\n===== {FixtureLabel(testFileName, expected)} =====");
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
                    if (postEntries == null)
                    {
                        _output.WriteLine($"  OUT OF SCOPE: {fileName}[{testEntry.Key}] — the fixture ships no post state for {hardfork}");
                        totalSkipped++;
                        continue;
                    }

                    foreach (var expected in postEntries)
                    {
                        var label = FixtureLabel(fileName, expected);

                        if (TryGetOutOfScopeReason(expected, out var outOfScopeReason))
                        {
                            _output.WriteLine($"  OUT OF SCOPE: {label} — {outOfScopeReason}");
                            totalSkipped++;
                            continue;
                        }

                        try
                        {
                            var result = await RunRecordingAsync(test, expected, HardforkNames.Parse(hardfork));
                            var expectsException = !string.IsNullOrEmpty(expected.ExpectException);

                            if (expectsException && result.IsValidationError)
                            {
                                if (ExpectedRejectionMatcher.Satisfies(expected.ExpectException, result.ErrorCode, out var rejectionDetail))
                                {
                                    _output.WriteLine($"  {label}: expected {expected.ExpectException} — got {rejectionDetail}");
                                    totalPassed++;
                                }
                                else
                                {
                                    failures.Add($"{label}: expected {expected.ExpectException}, rejected as TransactionError.{result.ErrorCode} — {rejectionDetail}");
                                    totalFailed++;
                                }
                            }
                            else if (expectsException && !result.IsValidationError)
                            {
                                failures.Add($"{label}: expected {expected.ExpectException} but tx was valid");
                                totalFailed++;
                            }
                            else if (!expectsException && result.IsValidationError)
                            {
                                failures.Add($"{label}: tx invalid: {result.Error ?? "unknown"}");
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
                                    failures.Add($"{label}: STATE ROOT MISMATCH expected={expected.Hash} actual=0x{result.StateRoot.ToHex()} (recorder accounts={result.RecordedAccounts}, gas={result.GasUsed}){diagSuffix}");
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
                            if (!string.IsNullOrEmpty(expected.ExpectException) &&
                                ExpectedRejectionMatcher.Satisfies(expected.ExpectException, ex, out var rejectionDetail))
                            {
                                _output.WriteLine($"  {label}: expected {expected.ExpectException} — got {rejectionDetail}");
                                totalPassed++;
                            }
                            else
                            {
                                failures.Add($"{label}: {ex.GetType().Name}: {ex.Message}");
                                totalFailed++;
                            }
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

        private static string FixtureLabel(string fileName, PostResult expected)
        {
            var indexes = expected.Indexes;
            return indexes == null
                ? $"{fileName}[no indexes]"
                : $"{fileName}[{indexes.Data},{indexes.Gas},{indexes.Value}]";
        }

        private static bool TryGetOutOfScopeReason(PostResult expected, out string reason)
        {
            if (string.IsNullOrEmpty(expected.TxBytes))
            {
                reason = "no txbytes: this pipeline replays a signed carrier — it decodes the RLP, " +
                         "recovers the EIP-7702 authorities from it and hands it to BlockExecutor — " +
                         "so an entry without one carries no transaction to record or execute";
                return true;
            }

            reason = null;
            return false;
        }

        private async Task<RecordingResult> RunRecordingAsync(GeneralStateTest test, PostResult expected, HardforkName fork)
        {
            var blockData = StateTestToWitnessConverter.Convert(test, expected, fork);
            blockData.ComputePostStateRoot = true;

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
            var executor = new TransactionExecutor(config: WithPluginBackends(SelectConfig(fork), fork));
            var ctx = TransactionContextFactory.FromBlockWitnessTransaction(
                blockData.Transactions[0], blockData, executionState);
            var execResult = await executor.ExecuteAsync(ctx);
            if (execResult.IsValidationError)
            {
                return new RecordingResult
                {
                    IsValidationError = true,
                    Error = execResult.Error,
                    ErrorCode = execResult.ErrorCode,
                    GasUsed = execResult.GasUsed
                };
            }
            var recordedAccountsCount = recorder.GetWitnessAccounts().Count;

            var blockResult = await Nethereum.EVM.Execution.BlockExecutor.ExecuteAsync(
                blockData,
                RlpBlockEncodingProvider.Instance,
                MainnetRegistry,
                new PatriciaStateRootCalculator(RlpBlockEncodingProvider.Instance));

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
                var addrKey = Nethereum.Util.EvmAddress.FromHex(addr);
                var actualAcct = finalES.AccountsState.ContainsKey(addrKey) ? finalES.AccountsState[addrKey] : null;

                var expectedBalance = string.IsNullOrEmpty(expected.Balance) ? BigInteger.Zero : expected.Balance.HexToBigInteger(false);
                var expectedNonce = string.IsNullOrEmpty(expected.Nonce) ? 0UL : (ulong)expected.Nonce.HexToBigInteger(false);
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
                if ((ulong)actualNonce != expectedNonce)
                    parts.Add($"nonce {expectedNonce}→{(ulong)actualNonce}");
                if (!actualCode.AreTheSame(expectedCode))
                    parts.Add($"code {expectedCode.Length}b→{actualCode.Length}b");

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

        internal static string GetTestVectorsPath()
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
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
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
            public TransactionError ErrorCode { get; set; }
            public string AccountDiff { get; set; }
        }
    }
}
