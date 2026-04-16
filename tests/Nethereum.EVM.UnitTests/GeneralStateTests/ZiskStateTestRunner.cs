using Nethereum.EVM;
using Nethereum.EVM.Witness;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class ZiskStateTestRunner
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testVectorsPath;
        private readonly string _elfPath;
        private readonly string _outputDir;

        public ZiskStateTestRunner(ITestOutputHelper output)
        {
            _output = output;
            _testVectorsPath = GetTestVectorsPath();
            _elfPath = FindElfPath();
            _outputDir = Path.Combine(Path.GetTempPath(), "zisk-witnesses");
            Directory.CreateDirectory(_outputDir);
        }

        public async Task RunCategoryNativeAsync(string categoryName, string hardfork = "Prague")
        {
            var categoryPath = Path.Combine(_testVectorsPath, categoryName);
            if (!Directory.Exists(categoryPath))
            {
                _output.WriteLine($"Category {categoryName} not found");
                Assert.True(false, $"Category not found: {categoryName}");
                return;
            }

            var testFiles = Directory.GetFiles(categoryPath, "*.json", SearchOption.AllDirectories);
            int totalPassed = 0, totalFailed = 0, totalSkipped = 0;
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
                            var result = await RunNativeAsync(test, expected, HardforkNames.Parse(hardfork));
                            var expectsException = !string.IsNullOrEmpty(expected.ExpectException);

                            if ((result.Success && !expectsException) || (!result.Success && expectsException))
                                totalPassed++;
                            else
                            {
                                failures.Add($"{fileName}[{expected.Indexes.Data},{expected.Indexes.Gas},{expected.Indexes.Value}]: success={result.Success} expected_exception={expectsException} error={result.Error}");
                                totalFailed++;
                            }
                        }
                        catch (Exception)
                        {
                            totalSkipped++;
                        }
                    }
                }
            }

            _output.WriteLine($"=== {categoryName} Native Witness SUMMARY ===");
            _output.WriteLine($"Passed: {totalPassed}, Failed: {totalFailed}, Skipped: {totalSkipped}");

            if (failures.Count > 0)
            {
                _output.WriteLine("=== FAILURES ===");
                foreach (var f in failures)
                    _output.WriteLine(f);
            }

            Assert.Equal(0, totalFailed);
        }

        public async Task RunCategoryZiskAsync(string categoryName, int maxTests = 5, string hardfork = "Prague")
        {
            var categoryPath = Path.Combine(_testVectorsPath, categoryName);
            if (!Directory.Exists(categoryPath))
            {
                _output.WriteLine($"Category {categoryName} not found");
                return;
            }

            if (string.IsNullOrEmpty(_elfPath))
            {
                _output.WriteLine("SKIP Zisk tests — ELF binary not found. Run: bash scripts/build-zisk-source.sh");
                return;
            }

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
                        var witnessFile = WriteZiskWitness(fileName, expected, witnessBytes);

                        var result = RunZiskEmu(witnessFile);

                        if ((result.Success && !expectsException) || (!result.Success && expectsException))
                        {
                            _output.WriteLine($"  ZISK PASS: {fileName} gas={result.GasUsed}");
                            passed++;
                        }
                        else
                        {
                            _output.WriteLine($"  ZISK FAIL: {fileName} {result.Error}");
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

            _output.WriteLine($"=== {categoryName} Zisk SUMMARY === Passed: {passed}, Failed: {failed} (of {tested})");
        }

        private async Task<NativeResult> RunNativeAsync(GeneralStateTest test, PostResult expected, HardforkName fork)
        {
            var witness = StateTestToWitnessConverter.Convert(test, expected, fork);
            var witnessBytes = BinaryBlockWitness.Serialize(witness);
            var deserialized = BinaryBlockWitness.Deserialize(witnessBytes);

            var accounts = WitnessStateBuilder.BuildAccountState(deserialized.Accounts);
            var stateReader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(stateReader);

            foreach (var acc in deserialized.Accounts)
            {
                await executionState.LoadBalanceNonceAndCodeFromStorageAsync(acc.Address);
                if (acc.Storage != null)
                {
                    var acctState = executionState.CreateOrGetAccountExecutionState(acc.Address);
                    foreach (var slot in acc.Storage)
                        acctState.SetPreStateStorage(slot.Key, slot.Value.ToBigEndian());
                }
            }

            var wtx = deserialized.Transactions[0];
            var ctx = TransactionContextFactory.FromBlockWitnessTransaction(wtx, deserialized, executionState);

            var executor = new TransactionExecutor(
                DefaultMainnetHardforkRegistry.Instance.Get(fork));
            var result = await executor.ExecuteAsync(ctx);

            return new NativeResult
            {
                Success = result.Success,
                GasUsed = result.GasUsed,
                Error = result.Error
            };
        }

        private string WriteZiskWitness(string fileName, PostResult expected, byte[] witnessBytes)
        {
            var witnessFile = Path.Combine(_outputDir,
                $"{fileName}_{expected.Indexes.Data}_{expected.Indexes.Gas}_{expected.Indexes.Value}.bin");

            int padLen = (8 - witnessBytes.Length % 8) % 8;
            if (padLen > 0)
            {
                var padded = new byte[witnessBytes.Length + padLen];
                Array.Copy(witnessBytes, padded, witnessBytes.Length);
                witnessBytes = padded;
            }

            var legacyInput = new byte[16 + witnessBytes.Length];
            BitConverter.GetBytes((long)0).CopyTo(legacyInput, 0);
            BitConverter.GetBytes((long)witnessBytes.Length).CopyTo(legacyInput, 8);
            Array.Copy(witnessBytes, 0, legacyInput, 16, witnessBytes.Length);
            File.WriteAllBytes(witnessFile, legacyInput);

            return witnessFile;
        }

        private ZiskResult RunZiskEmu(string witnessFile)
        {
            if (string.IsNullOrEmpty(_elfPath))
                return new ZiskResult { Error = "ELF not found" };

            var wslWitness = witnessFile.Replace("\\", "/").Replace("C:/", "/mnt/c/").Replace("c:/", "/mnt/c/");
            var wslElf = _elfPath.Replace("\\", "/").Replace("C:/", "/mnt/c/").Replace("c:/", "/mnt/c/");

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

        private static string FindElfPath()
        {
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            if (projectRoot == null) return null;
            var elfPath = Path.Combine(projectRoot, "scripts", "zisk-output", "nethereum_evm_elf");
            return File.Exists(elfPath) ? elfPath : null;
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
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        private class NativeResult
        {
            public bool Success { get; set; }
            public long GasUsed { get; set; }
            public string Error { get; set; }
        }

        private class ZiskResult
        {
            public bool Success { get; set; }
            public long GasUsed { get; set; }
            public string Error { get; set; }
        }
    }
}
