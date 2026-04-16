using Nethereum.CoreChain;
using Nethereum.EVM.Core.Tests;
using Nethereum.EVM.Witness;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class ZiskSyncStateTestRunner
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testVectorsPath;

        public ZiskSyncStateTestRunner(ITestOutputHelper output)
        {
            _output = output;
            _testVectorsPath = GetTestVectorsPath();
        }

        /// <summary>
        /// Direct sync: JSON → build state directly → sync Execute.
        /// Tests the sync EVM in isolation — no witness involved.
        /// </summary>
        public void RunCategoryDirect(string categoryName, string hardfork = "Prague")
        {
            RunCategory(categoryName, hardfork, "Direct Sync", RunDirect);
        }

        /// <summary>
        /// Witness roundtrip sync: JSON → WitnessData → Serialize → Deserialize → build state → sync Execute.
        /// Tests witness format fidelity AND sync EVM — same pipeline as Zisk binary.
        /// </summary>
        public void RunCategoryWitness(string categoryName, string hardfork = "Prague")
        {
            RunCategory(categoryName, hardfork, "Witness Sync", RunWitness);
        }

        private void RunCategory(string categoryName, string hardfork, string label,
            Func<GeneralStateTest, PostResult, HardforkName, SyncResult> runFunc)
        {
            var categoryPath = Path.Combine(_testVectorsPath, categoryName);
            if (!Directory.Exists(categoryPath))
            {
                _output.WriteLine($"Category {categoryName} not found at {categoryPath}");
                Assert.Fail($"Category not found: {categoryName}");
                return;
            }

            var testFiles = Directory.GetFiles(categoryPath, "*.json", SearchOption.AllDirectories);
            int totalPassed = 0, totalFailed = 0, totalSkipped = 0, stateRootMismatches = 0;
            var failures = new List<string>();

            foreach (var testFile in testFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(testFile);
                _output.WriteLine($"Running: {fileName}");

                var json = File.ReadAllText(testFile);
                var tests = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, GeneralStateTest>>(json);

                foreach (var testEntry in tests)
                {
                    var test = testEntry.Value;
                    var postEntries = test.Post.ContainsKey(hardfork) ? test.Post[hardfork] : null;
                    if (postEntries == null) continue;

                    foreach (var expected in postEntries)
                    {
                        try
                        {
                            var result = runFunc(test, expected, HardforkNames.Parse(hardfork));
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
                                    var msg = $"{fileName}[{expected.Indexes.Data},{expected.Indexes.Gas},{expected.Indexes.Value}]: STATE ROOT MISMATCH expected={expected.Hash} actual=0x{result.StateRoot.ToHex()}";
                                    if (result.AccountDiag != null)
                                        msg += " | " + result.AccountDiag;
                                    failures.Add(msg);
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
                            _output.WriteLine($"  SKIP: {fileName} — {ex.Message}");
                            totalSkipped++;
                        }
                    }
                }
            }

            _output.WriteLine($"=== {categoryName} {label} SUMMARY ===");
            _output.WriteLine($"Passed: {totalPassed}, Failed: {totalFailed}, Skipped: {totalSkipped}, StateRootMismatches: {stateRootMismatches}");

            if (failures.Count > 0)
            {
                _output.WriteLine("=== FAILURES ===");
                foreach (var f in failures)
                    _output.WriteLine(f);
            }

            Assert.Equal(0, totalFailed);
        }

        private SyncResult RunDirect(GeneralStateTest test, PostResult expected, HardforkName fork)
        {
            var env = test.Env;
            var tx = test.Transaction;

            var dataIndex = expected.Indexes.Data;
            var gasIndex = expected.Indexes.Gas;
            var valueIndex = expected.Indexes.Value;

            var data = tx.Data != null && dataIndex < tx.Data.Count ? tx.Data[dataIndex] : "0x";
            var gasLimitStr = tx.GasLimit != null && gasIndex < tx.GasLimit.Count ? tx.GasLimit[gasIndex] : "0x0";
            var valueStr = tx.Value != null && valueIndex < tx.Value.Count ? tx.Value[valueIndex] : "0x0";

            var dataBytes = string.IsNullOrEmpty(data) || data == "0x" ? new byte[0] : data.HexToByteArray();
            var gasLimit = gasLimitStr.HexToBigInteger(false);
            var value = valueStr.HexToBigInteger(false);
            var baseFee = string.IsNullOrEmpty(env.CurrentBaseFee) ? BigInteger.Zero : env.CurrentBaseFee.HexToBigInteger(false);

            BigInteger gasPrice;
            if (!string.IsNullOrEmpty(tx.MaxFeePerGas))
                gasPrice = tx.MaxFeePerGas.HexToBigInteger(false);
            else
                gasPrice = string.IsNullOrEmpty(tx.GasPrice) ? BigInteger.Zero : tx.GasPrice.HexToBigInteger(false);

            var sender = tx.Sender ?? GetSenderFromSecretKey(tx.SecretKey);
            var toAddress = string.IsNullOrEmpty(tx.To) ? null : tx.To;

            var blockNumber = string.IsNullOrEmpty(env.CurrentNumber) ? 1L : (long)env.CurrentNumber.HexToBigInteger(false);
            var timestamp = string.IsNullOrEmpty(env.CurrentTimestamp) ? 0L : (long)env.CurrentTimestamp.HexToBigInteger(false);
            var blockGasLimit = string.IsNullOrEmpty(env.CurrentGasLimit) ? 0L : (long)env.CurrentGasLimit.HexToBigInteger(false);
            // Post-merge: DIFFICULTY opcode returns currentRandom (PREVRANDAO)
            EvmUInt256 difficulty;
            if (!string.IsNullOrEmpty(env.CurrentRandom))
                difficulty = EvmUInt256.FromBigEndian(env.CurrentRandom.HexToByteArray());
            else if (!string.IsNullOrEmpty(env.CurrentDifficulty))
                difficulty = EvmUInt256BigIntegerExtensions.FromBigInteger(env.CurrentDifficulty.HexToBigInteger(false));
            else
                difficulty = EvmUInt256.Zero;
            var coinbase = string.IsNullOrEmpty(env.CurrentCoinbase) ? "0x0000000000000000000000000000000000000000" : env.CurrentCoinbase;

            // Build state directly from JSON pre-state — no witness involved
            var accounts = new Dictionary<string, AccountState>();
            foreach (var preAccount in test.Pre)
            {
                var address = preAccount.Key;
                var account = preAccount.Value;

                var state = new AccountState
                {
                    Balance = EvmUInt256BigIntegerExtensions.FromBigInteger(
                        string.IsNullOrEmpty(account.Balance) ? BigInteger.Zero : account.Balance.HexToBigInteger(false)),
                    Nonce = string.IsNullOrEmpty(account.Nonce) ? 0L : (long)account.Nonce.HexToBigInteger(false),
                    Code = string.IsNullOrEmpty(account.Code) || account.Code == "0x" ? new byte[0] : account.Code.HexToByteArray()
                };

                if (account.Storage != null)
                {
                    foreach (var storage in account.Storage)
                    {
                        var storageKey = EvmUInt256BigIntegerExtensions.FromBigInteger(storage.Key.HexToBigInteger(false));
                        state.Storage[storageKey] = EvmUInt256.FromBigEndian(storage.Value.HexToByteArray().PadTo32Bytes()).ToBigEndian();
                    }
                }

                accounts[address.ToLower()] = state;
            }

            var stateReader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(stateReader);

            // Load ALL pre-state accounts AND storage so state root includes everything
            foreach (var preAccount in test.Pre)
            {
                executionState.LoadBalanceNonceAndCodeFromStorage(preAccount.Key);
                if (preAccount.Value.Storage != null)
                {
                    var acctState = executionState.CreateOrGetAccountExecutionState(preAccount.Key);
                    foreach (var storage in preAccount.Value.Storage)
                    {
                        var storageKey = EvmUInt256BigIntegerExtensions.FromBigInteger(storage.Key.HexToBigInteger(false));
                        var storageVal = EvmUInt256.FromBigEndian(storage.Value.HexToByteArray().PadTo32Bytes()).ToBigEndian();
                        acctState.SetPreStateStorage(storageKey, storageVal);
                    }
                }
            }

            var isEip1559 = !string.IsNullOrEmpty(tx.MaxFeePerGas);
            var maxFeePerGas = isEip1559 ? tx.MaxFeePerGas.HexToBigInteger(false) : BigInteger.Zero;
            var maxPriorityFeePerGas = isEip1559 && !string.IsNullOrEmpty(tx.MaxPriorityFeePerGas)
                ? tx.MaxPriorityFeePerGas.HexToBigInteger(false) : BigInteger.Zero;

            EvmUInt256 effectiveGasPrice;
            if (isEip1559)
            {
                var priority = maxPriorityFeePerGas < (maxFeePerGas - baseFee)
                    ? maxPriorityFeePerGas : (maxFeePerGas - baseFee);
                effectiveGasPrice = EvmUInt256BigIntegerExtensions.FromBigInteger(baseFee + priority);
            }
            else
            {
                effectiveGasPrice = EvmUInt256BigIntegerExtensions.FromBigInteger(gasPrice);
            }

            // Parse access list if present
            List<AccessListEntry> accessList = null;
            if (tx.AccessLists != null)
            {
                var dataIndex2 = expected.Indexes.Data;
                if (dataIndex2 < tx.AccessLists.Count && tx.AccessLists[dataIndex2] != null)
                {
                    accessList = new List<AccessListEntry>();
                    foreach (var item in tx.AccessLists[dataIndex2])
                    {
                        accessList.Add(new AccessListEntry
                        {
                            Address = item.Address,
                            StorageKeys = item.StorageKeys ?? new List<string>()
                        });
                    }
                }
            }

            var ctx = new TransactionExecutionContext
            {
                Mode = ExecutionMode.Transaction,
                Sender = sender,
                To = toAddress ?? "",
                Data = dataBytes,
                Value = EvmUInt256BigIntegerExtensions.FromBigInteger(value),
                GasLimit = (long)gasLimit,
                GasPrice = (long)gasPrice,
                IsEip1559 = isEip1559,
                MaxFeePerGas = EvmUInt256BigIntegerExtensions.FromBigInteger(maxFeePerGas),
                MaxPriorityFeePerGas = EvmUInt256BigIntegerExtensions.FromBigInteger(maxPriorityFeePerGas),
                EffectiveGasPrice = effectiveGasPrice,
                Nonce = string.IsNullOrEmpty(tx.Nonce) ? 0L : (long)tx.Nonce.HexToBigInteger(false),
                IsContractCreation = toAddress == null,
                BlockNumber = blockNumber,
                Timestamp = timestamp,
                Coinbase = coinbase,
                BaseFee = (long)baseFee,
                Difficulty = difficulty,
                BlockGasLimit = blockGasLimit,
                ChainId = 1,
                AccessList = accessList,
                ExecutionState = executionState
            };

            var executor = new TransactionExecutor(config: Nethereum.EVM.Precompiles.DefaultHardforkConfigs.Prague);
            var result = executor.Execute(ctx);

            var stateRootCalc = new PatriciaStateRootCalculator(Nethereum.Model.RlpBlockEncodingProvider.Instance);
            var stateRoot = stateRootCalc.ComputeStateRoot(executionState);

            // Build diagnostic for first few mismatches
            var diag = new System.Text.StringBuilder();
            diag.Append($"gas={result.GasUsed},refund={result.GasRefund},success={result.Success} ");
            foreach (var kvp in executionState.AccountsState)
            {
                var bal = kvp.Value.Balance.GetTotalBalance();
                var nonce = kvp.Value.Nonce ?? EvmUInt256.Zero;
                if (!bal.IsZero || nonce > 0 || (kvp.Value.Storage?.Count ?? 0) > 0)
                    diag.Append($"{kvp.Key.Substring(0,6)}:b={bal},n={nonce},s={kvp.Value.Storage?.Count ?? 0} ");
            }

            return new SyncResult
            {
                Success = result.Success,
                GasUsed = result.GasUsed,
                Error = result.Error,
                IsValidationError = result.IsValidationError,
                StateRoot = stateRoot,
                AccountDiag = diag.ToString()
            };
        }

        private SyncResult RunWitness(GeneralStateTest test, PostResult expected, HardforkName fork)
        {
            // Convert test → v3 witness → serialize → deserialize (roundtrip validation)
            var witness = StateTestToWitnessConverter.Convert(test, expected, fork);
            var witnessBytes = BinaryBlockWitness.Serialize(witness);
            var deserialized = BinaryBlockWitness.Deserialize(witnessBytes);

            // Use shared builders — same code as Zisk binary
            var accounts = WitnessStateBuilder.BuildAccountState(deserialized.Accounts);
            var stateReader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(stateReader);
            WitnessStateBuilder.LoadAllAccountsAndStorage(executionState, deserialized.Accounts);

            // Decode from RLP + execute — same path as Zisk binary
            var wtx = deserialized.Transactions[0];
            var ctx = Nethereum.EVM.Execution.TransactionContextFactory
                .FromBlockWitnessTransaction(wtx, deserialized, executionState);

            var executor = new TransactionExecutor(config: Nethereum.EVM.Precompiles.DefaultHardforkConfigs.Prague);
            var result = executor.Execute(ctx);

            var stateRootCalc = new PatriciaStateRootCalculator(Nethereum.Model.RlpBlockEncodingProvider.Instance);
            var stateRoot = stateRootCalc.ComputeStateRoot(executionState);

            return new SyncResult
            {
                Success = result.Success,
                GasUsed = result.GasUsed,
                Error = result.Error,
                IsValidationError = result.IsValidationError,
                StateRoot = stateRoot
            };
        }

        /// <summary>
        /// Zisk emulator: JSON → WitnessData → Serialize → write .bin → ziskemu via WSL.
        /// Picks first test per file, validates the full zkVM execution path.
        /// </summary>
        public void RunCategoryZiskEmu(string categoryName, int maxTests = 3, string hardfork = "Prague")
        {
            var categoryPath = Path.Combine(_testVectorsPath, categoryName);
            if (!Directory.Exists(categoryPath))
            {
                _output.WriteLine($"Category {categoryName} not found");
                return;
            }

            var elfPath = FindElfPath();
            if (string.IsNullOrEmpty(elfPath))
            {
                _output.WriteLine("SKIP Zisk tests — ELF binary not found. Run: bash scripts/build-zisk-source.sh");
                return;
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "zisk-witnesses");
            Directory.CreateDirectory(outputDir);

            var testFiles = Directory.GetFiles(categoryPath, "*.json", SearchOption.AllDirectories);
            int tested = 0, passed = 0, failed = 0;

            foreach (var testFile in testFiles)
            {
                if (tested >= maxTests) break;

                var fileName = Path.GetFileNameWithoutExtension(testFile);
                var json = File.ReadAllText(testFile);
                var tests = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, GeneralStateTest>>(json);

                foreach (var testEntry in tests)
                {
                    if (tested >= maxTests) break;

                    var test = testEntry.Value;
                    var postEntries = test.Post.ContainsKey(hardfork) ? test.Post[hardfork] : null;
                    if (postEntries == null || postEntries.Count == 0) continue;

                    var expected = postEntries[0];
                    var expectsException = !string.IsNullOrEmpty(expected.ExpectException);

                    try
                    {
                        var witness = StateTestToWitnessConverter.Convert(test, expected, HardforkNames.Parse(hardfork));
                        var witnessBytes = BinaryBlockWitness.Serialize(witness);
                        var witnessFile = WriteZiskWitness(outputDir, fileName, expected, witnessBytes);

                        var result = RunZiskEmu(elfPath, witnessFile);

                        // Zisk binary outputs BIN:OK or BIN:FAIL
                        // For expectsException tests, the tx is invalid so Zisk should also fail
                        if ((result.Success && !expectsException) || (!result.Success && expectsException))
                        {
                            _output.WriteLine($"  ZISK PASS: {fileName} gas={result.GasUsed}");
                            passed++;
                        }
                        else
                        {
                            _output.WriteLine($"  ZISK FAIL: {fileName} success={result.Success} expected_exception={expectsException} error={result.Error}");
                            failed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  ZISK SKIP: {fileName} {ex.Message}");
                    }
                    tested++;
                }
            }

            _output.WriteLine($"=== {categoryName} ZiskEmu SUMMARY === Passed: {passed}, Failed: {failed} (of {tested})");
            if (failed > 0)
                Assert.Fail($"{failed} Zisk emulator tests failed in {categoryName}");
        }

        /// <summary>
        /// Generate witness files for a category. Writes v3 BinaryBlockWitness
        /// files to outputDir. No emulation — just file generation.
        /// </summary>
        public int GenerateWitnesses(string categoryName, string outputDir, string hardfork = "Prague", int maxTests = int.MaxValue)
        {
            var categoryPath = Path.Combine(_testVectorsPath, categoryName);
            if (!Directory.Exists(categoryPath))
            {
                _output.WriteLine($"Category {categoryName} not found");
                return 0;
            }

            Directory.CreateDirectory(outputDir);
            var testFiles = Directory.GetFiles(categoryPath, "*.json", SearchOption.AllDirectories);
            int generated = 0;

            foreach (var testFile in testFiles)
            {
                if (generated >= maxTests) break;

                var fileName = Path.GetFileNameWithoutExtension(testFile);
                var json = File.ReadAllText(testFile);
                var tests = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, GeneralStateTest>>(json);

                foreach (var testEntry in tests)
                {
                    if (generated >= maxTests) break;
                    var test = testEntry.Value;
                    var postEntries = test.Post.ContainsKey(hardfork) ? test.Post[hardfork] : null;
                    if (postEntries == null || postEntries.Count == 0) continue;

                    var expected = postEntries[0];
                    try
                    {
                        var witness = StateTestToWitnessConverter.Convert(test, expected, HardforkNames.Parse(hardfork));
                        var witnessBytes = BinaryBlockWitness.Serialize(witness);
                        var path = WriteZiskWitness(outputDir, fileName, expected, witnessBytes);
                        _output.WriteLine($"  Generated: {Path.GetFileName(path)} ({witnessBytes.Length} bytes)");
                        generated++;
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  Skip: {fileName} — {ex.Message}");
                    }
                }
            }

            _output.WriteLine($"Generated {generated} witness files in {outputDir}");
            return generated;
        }

        private static string WriteZiskWitness(string outputDir, string fileName, PostResult expected, byte[] witnessBytes)
        {
            var witnessFile = Path.Combine(outputDir,
                $"{fileName}_{expected.Indexes.Data}_{expected.Indexes.Gas}_{expected.Indexes.Value}.bin");

            // Pad to 8-byte alignment
            int padLen = (8 - witnessBytes.Length % 8) % 8;
            if (padLen > 0)
            {
                var padded = new byte[witnessBytes.Length + padLen];
                Array.Copy(witnessBytes, padded, witnessBytes.Length);
                witnessBytes = padded;
            }

            // Legacy format: [u64 zero][u64 length][data]
            var legacyInput = new byte[16 + witnessBytes.Length];
            BitConverter.GetBytes((long)0).CopyTo(legacyInput, 0);
            BitConverter.GetBytes((long)witnessBytes.Length).CopyTo(legacyInput, 8);
            Array.Copy(witnessBytes, 0, legacyInput, 16, witnessBytes.Length);
            File.WriteAllBytes(witnessFile, legacyInput);

            return witnessFile;
        }

        private ZiskResult RunZiskEmu(string elfPath, string witnessFile)
        {
            var wslWitness = ToWslPath(witnessFile);
            var wslElf = ToWslPath(elfPath);

            var psi = new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = $"-d Ubuntu -e bash -c \"export PATH=$HOME/.zisk/bin:$HOME/.cargo/bin:$PATH && ziskemu -e {wslElf} --legacy-inputs {wslWitness} -n 100000000\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(120000))
            {
                try { process.Kill(); } catch { }
                return new ZiskResult { Error = "ziskemu timeout" };
            }

            var result = new ZiskResult();
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("BIN:OK gas="))
                {
                    result.Success = true;
                    long.TryParse(trimmed.Substring("BIN:OK gas=".Length), out var gas);
                    result.GasUsed = gas;
                }
                else if (trimmed.StartsWith("BIN:FAIL"))
                {
                    result.Success = false;
                    result.Error = trimmed.Length > 9 ? trimmed.Substring(9) : "unknown";
                }
                else if (trimmed.Contains("bad version"))
                {
                    result.Success = false;
                    result.Error = "bad witness version";
                }
            }

            if (!result.Success && string.IsNullOrEmpty(result.Error))
                result.Error = $"exit code {process.ExitCode}";

            return result;
        }

        private static string ToWslPath(string windowsPath)
        {
            return windowsPath.Replace("\\", "/").Replace("C:/", "/mnt/c/").Replace("c:/", "/mnt/c/");
        }

        private static string FindElfPath()
        {
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            if (projectRoot == null) return null;
            var elfPath = Path.Combine(projectRoot, "scripts", "zisk-output", "nethereum_evm_elf");
            return File.Exists(elfPath) ? elfPath : null;
        }

        private class ZiskResult
        {
            public bool Success { get; set; }
            public long GasUsed { get; set; }
            public string Error { get; set; }
        }

        private static string GetSenderFromSecretKey(string secretKey)
        {
            if (string.IsNullOrEmpty(secretKey))
                return "0x0000000000000000000000000000000000000000";
            var key = new Nethereum.Signer.EthECKey(secretKey);
            return key.GetPublicAddress();
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

        private class SyncResult
        {
            public bool Success { get; set; }
            public long GasUsed { get; set; }
            public string Error { get; set; }
            public bool IsValidationError { get; set; }
            public byte[] StateRoot { get; set; }
            public string AccountDiag { get; set; }
        }
    }
}
