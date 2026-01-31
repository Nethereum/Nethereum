using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class TraceComparerTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TraceComparer _comparer;
        private readonly GeneralStateTestRunner _runner;
        private readonly string _reportPath;

        public TraceComparerTests(ITestOutputHelper output)
        {
            _output = output;
            _comparer = new TraceComparer();
            _runner = new GeneralStateTestRunner(output, targetHardfork: "Prague");
            _reportPath = GetReportPath();
        }

        private string GetReportPath()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "TRACE_DIVERGENCE_REPORT.md")))
            {
                dir = Directory.GetParent(dir)?.FullName;
            }
            return dir != null ? Path.Combine(dir, "TRACE_DIVERGENCE_REPORT.md") : null;
        }

        [Fact]
        public void TraceComparer_ParseGethTrace_ParsesCorrectly()
        {
            var gethJson = @"[
                {""pc"": 0, ""op"": ""PUSH1"", ""gas"": 30000, ""gasCost"": 3, ""depth"": 1, ""stack"": [], ""memory"": [], ""storage"": {}},
                {""pc"": 2, ""op"": ""PUSH1"", ""gas"": 29997, ""gasCost"": 3, ""depth"": 1, ""stack"": [""0000000000000000000000000000000000000000000000000000000000000080""], ""memory"": [], ""storage"": {}}
            ]";

            var steps = _comparer.ParseGethTrace(gethJson);

            Assert.Equal(2, steps.Count);
            Assert.Equal(0, steps[0].PC);
            Assert.Equal("PUSH1", steps[0].Op);
            Assert.Equal(30000, steps[0].Gas);
            Assert.Equal(3, steps[0].GasCost);
            Assert.Equal(1, steps[0].Depth);
            Assert.Empty(steps[0].Stack);

            Assert.Equal(2, steps[1].PC);
            Assert.Single(steps[1].Stack);
        }

        [Fact]
        public void TraceComparer_Compare_DetectsMatch()
        {
            var geth = new List<GethTraceStep>
            {
                new GethTraceStep { PC = 0, Op = "PUSH1", Gas = 30000, GasCost = 3, Depth = 1, Stack = new List<string>() },
                new GethTraceStep { PC = 2, Op = "PUSH1", Gas = 29997, GasCost = 3, Depth = 1, Stack = new List<string> { "80" } }
            };

            var neth = new List<NethTraceStep>
            {
                new NethTraceStep { PC = 0, Op = "PUSH1", Gas = 30000, GasCost = 3, Depth = 1, Stack = new List<string>() },
                new NethTraceStep { PC = 2, Op = "PUSH1", Gas = 29997, GasCost = 3, Depth = 1, Stack = new List<string> { "80" } }
            };

            var result = _comparer.Compare(geth, neth);

            Assert.False(result.HasDivergence);
            Assert.Equal("NONE", result.DivergenceReason);
            Assert.Equal(2, result.MatchingSteps);
        }

        [Fact]
        public void TraceComparer_Compare_DetectsGasDivergence()
        {
            var geth = new List<GethTraceStep>
            {
                new GethTraceStep { PC = 0, Op = "PUSH1", Gas = 30000, GasCost = 3, Depth = 1, Stack = new List<string>() },
                new GethTraceStep { PC = 2, Op = "SLOAD", Gas = 29997, GasCost = 2100, Depth = 1, Stack = new List<string>() }
            };

            var neth = new List<NethTraceStep>
            {
                new NethTraceStep { PC = 0, Op = "PUSH1", Gas = 30000, GasCost = 3, Depth = 1, Stack = new List<string>() },
                new NethTraceStep { PC = 2, Op = "SLOAD", Gas = 29997, GasCost = 100, Depth = 1, Stack = new List<string>() }
            };

            var result = _comparer.Compare(geth, neth);

            Assert.True(result.HasDivergence);
            Assert.Equal(2, result.FirstDivergenceStep);
            Assert.Contains("COST", result.DivergenceReason);
        }

        [Fact]
        public void TraceComparer_Compare_DetectsDepthDivergence()
        {
            var geth = new List<GethTraceStep>
            {
                new GethTraceStep { PC = 0, Op = "CALL", Gas = 30000, GasCost = 100, Depth = 1, Stack = new List<string>() },
                new GethTraceStep { PC = 0, Op = "PUSH1", Gas = 25000, GasCost = 3, Depth = 2, Stack = new List<string>() }
            };

            var neth = new List<NethTraceStep>
            {
                new NethTraceStep { PC = 0, Op = "CALL", Gas = 30000, GasCost = 100, Depth = 1, Stack = new List<string>() },
                new NethTraceStep { PC = 0, Op = "PUSH1", Gas = 25000, GasCost = 3, Depth = 1, Stack = new List<string>() }
            };

            var result = _comparer.Compare(geth, neth);

            Assert.True(result.HasDivergence);
            Assert.Equal(2, result.FirstDivergenceStep);
            Assert.Contains("DEPTH", result.DivergenceReason);
        }

        [Fact]
        public void TraceComparer_GenerateReport_ProducesMarkdown()
        {
            var geth = new List<GethTraceStep>
            {
                new GethTraceStep { PC = 0, Op = "PUSH1", Gas = 30000, GasCost = 3, Depth = 1, Stack = new List<string>() }
            };

            var neth = new List<NethTraceStep>
            {
                new NethTraceStep { PC = 0, Op = "PUSH1", Gas = 29000, GasCost = 3, Depth = 1, Stack = new List<string>() }
            };

            var result = _comparer.Compare(geth, neth);
            var report = _comparer.GenerateReport(result, "TestCase1");

            Assert.Contains("# EVM Trace Divergence Report", report);
            Assert.Contains("TestCase1", report);
            Assert.Contains("<<<GAS>>>", report);
        }

        [Fact(Skip = "Run manually to compare specific test with traces")]
        public async Task CompareSpecificTest_WithTraces()
        {
            var testDir = FindTestVectorsDirectory();
            if (testDir == null)
            {
                _output.WriteLine("Test vectors directory not found");
                return;
            }

            var testFile = Path.Combine(testDir, "stCallCodes", "callcallcallcode_001.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                return;
            }

            var testResult = await _runner.RunTestWithTraceAsync(testFile);

            foreach (var singleResult in testResult.Results.Where(r => !r.Skipped))
            {
                _output.WriteLine($"\n=== {singleResult.TestName} [d={singleResult.DataIndex},g={singleResult.GasIndex},v={singleResult.ValueIndex}] ===");
                _output.WriteLine($"Passed: {singleResult.Passed}");

                if (singleResult.Traces != null && singleResult.Traces.Count > 0)
                {
                    _output.WriteLine($"Trace steps captured: {singleResult.Traces.Count}");

                    var nethSteps = _comparer.NormalizeNethTrace(singleResult.Traces);
                    _output.WriteLine("\nFirst 10 Nethereum trace steps:");
                    foreach (var step in nethSteps.Take(10))
                    {
                        _output.WriteLine($"  Step PC={step.PC} Op={step.Op} Gas={step.Gas} Cost={step.GasCost} Depth={step.Depth}");
                    }
                }

                if (!singleResult.Passed && singleResult.AccountDiffs != null)
                {
                    _output.WriteLine("\nAccount diffs:");
                    foreach (var diff in singleResult.AccountDiffs.Take(10))
                    {
                        _output.WriteLine($"  {diff}");
                    }
                }
            }
        }

        [Fact(Skip = "Run manually to generate full divergence report")]
        public async Task GenerateDivergenceReport_ForFailingTest()
        {
            var testDir = FindTestVectorsDirectory();
            if (testDir == null)
            {
                _output.WriteLine("Test vectors directory not found");
                return;
            }

            var testFiles = new[]
            {
                "stCallCodes/callcallcallcode_001.json",
                "stCallCodes/callcallcodecall_010.json",
            };

            var reportBuilder = new StringBuilder();
            reportBuilder.AppendLine();
            reportBuilder.AppendLine($"## Trace Analysis Run - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            reportBuilder.AppendLine();

            foreach (var relPath in testFiles)
            {
                var testFile = Path.Combine(testDir, relPath);
                if (!File.Exists(testFile))
                {
                    _output.WriteLine($"Skipping (not found): {relPath}");
                    continue;
                }

                _output.WriteLine($"\nAnalyzing: {relPath}");
                var testResult = await _runner.RunTestWithTraceAsync(testFile);

                foreach (var singleResult in testResult.Results.Where(r => !r.Skipped && !r.Passed))
                {
                    if (singleResult.Traces == null || singleResult.Traces.Count == 0)
                    {
                        reportBuilder.AppendLine($"### {singleResult.TestName} - NO TRACES CAPTURED");
                        continue;
                    }

                    var nethSteps = _comparer.NormalizeNethTrace(singleResult.Traces);

                    reportBuilder.AppendLine($"### {singleResult.TestName}");
                    reportBuilder.AppendLine();
                    reportBuilder.AppendLine($"- **File:** {relPath}");
                    reportBuilder.AppendLine($"- **Indices:** d={singleResult.DataIndex}, g={singleResult.GasIndex}, v={singleResult.ValueIndex}");
                    reportBuilder.AppendLine($"- **Nethereum Steps:** {nethSteps.Count}");
                    reportBuilder.AppendLine($"- **State Root:** Expected={singleResult.ExpectedStateRoot}, Actual={singleResult.ActualStateRoot}");
                    reportBuilder.AppendLine();

                    if (singleResult.AccountDiffs != null && singleResult.AccountDiffs.Count > 0)
                    {
                        reportBuilder.AppendLine("**Account Differences:**");
                        foreach (var diff in singleResult.AccountDiffs.Take(5))
                        {
                            reportBuilder.AppendLine($"- {diff}");
                        }
                        reportBuilder.AppendLine();
                    }

                    reportBuilder.AppendLine("**Last 20 Trace Steps:**");
                    reportBuilder.AppendLine("```");
                    reportBuilder.AppendLine("Step  | Depth | PC    | Op           | Gas        | Cost   |");
                    reportBuilder.AppendLine("------|-------|-------|--------------|------------|--------|");
                    foreach (var step in nethSteps.TakeLast(20))
                    {
                        reportBuilder.AppendLine(
                            $"{step.VMTraceStep,5} | " +
                            $"{step.Depth,5} | " +
                            $"{step.PC,5} | " +
                            $"{step.Op,-12} | " +
                            $"{step.Gas,10} | " +
                            $"{step.GasCost,6} |");
                    }
                    reportBuilder.AppendLine("```");
                    reportBuilder.AppendLine();

                    reportBuilder.AppendLine("**Analysis Required:**");
                    reportBuilder.AppendLine("1. WHERE: [fill in]");
                    reportBuilder.AppendLine("2. WHAT: [fill in]");
                    reportBuilder.AppendLine("3. WHY: [fill in]");
                    reportBuilder.AppendLine("4. WHICH file/line: [fill in]");
                    reportBuilder.AppendLine("5. FIX: [fill in]");
                    reportBuilder.AppendLine();
                    reportBuilder.AppendLine("---");
                    reportBuilder.AppendLine();
                }
            }

            if (_reportPath != null)
            {
                File.AppendAllText(_reportPath, reportBuilder.ToString());
                _output.WriteLine($"\nReport appended to: {_reportPath}");
            }

            _output.WriteLine(reportBuilder.ToString());
        }

        [Fact(Skip = "Run manually to compare with actual geth trace file")]
        public async Task CompareWithGethTraceFile()
        {
            var gethTraceFile = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "EVM", "Traces", "0xb9f4e6e5c90329a43da70ced8e8974c3fa34e67e32283bfa82778296fa79dd98.json");

            if (!File.Exists(gethTraceFile))
            {
                var altPath = FindGethTraceFile();
                if (altPath == null)
                {
                    _output.WriteLine("No geth trace file found");
                    return;
                }
                gethTraceFile = altPath;
            }

            _output.WriteLine($"Loading geth trace from: {gethTraceFile}");
            var gethJson = File.ReadAllText(gethTraceFile);
            var gethSteps = _comparer.ParseGethTrace(gethJson);

            _output.WriteLine($"Parsed {gethSteps.Count} geth trace steps");
            _output.WriteLine("\nFirst 10 geth steps:");
            foreach (var step in gethSteps.Take(10))
            {
                _output.WriteLine($"  PC={step.PC} Op={step.Op} Gas={step.Gas} Cost={step.GasCost} Depth={step.Depth}");
            }
        }

        [Fact]
        public async Task RunSingleTestWithDetailedTrace()
        {
            var testDir = FindTestVectorsDirectory();
            if (testDir == null)
            {
                _output.WriteLine("Test vectors directory not found - skipping");
                return;
            }

            var testFile = Path.Combine(testDir, "stExample", "add11.json");
            if (!File.Exists(testFile))
            {
                testFile = Directory.GetFiles(testDir, "*.json", SearchOption.AllDirectories).FirstOrDefault();
            }

            if (testFile == null || !File.Exists(testFile))
            {
                _output.WriteLine("No test file found - skipping");
                return;
            }

            _output.WriteLine($"Running test: {testFile}");
            var testResult = await _runner.RunTestWithTraceAsync(testFile);

            foreach (var result in testResult.Results)
            {
                _output.WriteLine($"\nTest: {result.TestName}");
                _output.WriteLine($"  Passed: {result.Passed}");
                _output.WriteLine($"  Skipped: {result.Skipped} {result.SkipReason}");

                if (result.Traces != null)
                {
                    _output.WriteLine($"  Trace steps: {result.Traces.Count}");
                    var nethSteps = _comparer.NormalizeNethTrace(result.Traces);
                    _output.WriteLine($"  Normalized steps: {nethSteps.Count}");
                }
            }
        }

        private string FindTestVectorsDirectory()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var searchPaths = new[]
            {
                Path.Combine(baseDir, "Tests", "GeneralStateTests"),
                Path.Combine(baseDir, "..", "..", "..", "Tests", "GeneralStateTests"),
                Path.Combine(baseDir, "..", "..", "..", "..", "tests", "Nethereum.EVM.UnitTests", "Tests", "GeneralStateTests"),
            };

            foreach (var path in searchPaths)
            {
                if (Directory.Exists(path))
                    return Path.GetFullPath(path);
            }

            var dir = baseDir;
            while (dir != null)
            {
                var candidate = Path.Combine(dir, "tests", "Nethereum.EVM.UnitTests", "Tests", "GeneralStateTests");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = Directory.GetParent(dir)?.FullName;
            }

            return null;
        }

        private string FindGethTraceFile()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = baseDir;
            while (dir != null)
            {
                var tracesDir = Path.Combine(dir, "tests", "Nethereum.Contracts.IntegrationTests", "EVM", "Traces");
                if (Directory.Exists(tracesDir))
                {
                    var files = Directory.GetFiles(tracesDir, "*.json");
                    if (files.Length > 0)
                        return files[0];
                }
                dir = Directory.GetParent(dir)?.FullName;
            }
            return null;
        }

        [Fact]
        public void TraceValidator_Validate_DetectsMatch()
        {
            var geth = new List<GethTraceStep>
            {
                new GethTraceStep { PC = 0, Op = "PUSH1", Gas = 30000, GasCost = 3, Depth = 1, Stack = new List<string>() },
                new GethTraceStep { PC = 2, Op = "PUSH1", Gas = 29997, GasCost = 3, Depth = 1, Stack = new List<string> { "80" } }
            };

            var neth = new List<NethTraceStep>
            {
                new NethTraceStep { PC = 0, Op = "PUSH1", Gas = 30000, GasCost = 3, Depth = 0, Stack = new List<string>() },
                new NethTraceStep { PC = 2, Op = "PUSH1", Gas = 29997, GasCost = 3, Depth = 0, Stack = new List<string> { "80" } }
            };

            var validator = new TraceValidator();
            var result = validator.Validate(geth, neth);

            Assert.True(result.IsValid, result.ToString());
            Assert.Equal(2, result.MatchedSteps);
            Assert.Null(result.FirstMismatch);
        }

        [Fact]
        public void TraceValidator_Validate_DetectsDepthMismatch()
        {
            var geth = new List<GethTraceStep>
            {
                new GethTraceStep { PC = 0, Op = "PUSH1", Gas = 30000, GasCost = 3, Depth = 1, Stack = new List<string>() },
                new GethTraceStep { PC = 2, Op = "CALL", Gas = 29997, GasCost = 100, Depth = 2, Stack = new List<string>() }
            };

            var neth = new List<NethTraceStep>
            {
                new NethTraceStep { PC = 0, Op = "PUSH1", Gas = 30000, GasCost = 3, Depth = 0, Stack = new List<string>() },
                new NethTraceStep { PC = 2, Op = "CALL", Gas = 29997, GasCost = 100, Depth = 0, Stack = new List<string>() }
            };

            var validator = new TraceValidator();
            var result = validator.Validate(geth, neth);

            Assert.False(result.IsValid);
            Assert.Equal(1, result.FirstMismatch.StepIndex);
            Assert.Equal("DEPTH", result.FirstMismatch.Field);
        }

        [Fact]
        public void TraceValidator_Validate_DetectsGasCostMismatch()
        {
            var geth = new List<GethTraceStep>
            {
                new GethTraceStep { PC = 0, Op = "SLOAD", Gas = 30000, GasCost = 2100, Depth = 1, Stack = new List<string>() }
            };

            var neth = new List<NethTraceStep>
            {
                new NethTraceStep { PC = 0, Op = "SLOAD", Gas = 30000, GasCost = 100, Depth = 0, Stack = new List<string>() }
            };

            var validator = new TraceValidator();
            var result = validator.Validate(geth, neth);

            Assert.False(result.IsValid);
            Assert.Equal("GAS_COST", result.FirstMismatch.Field);
            Assert.Equal("2100", result.FirstMismatch.GethValue);
            Assert.Equal("100", result.FirstMismatch.NethValue);
        }

        [Fact]
        public void TraceValidator_Validate_SkipsGasCostForCallOpcodes()
        {
            var geth = new List<GethTraceStep>
            {
                new GethTraceStep { PC = 0, Op = "CALL", Gas = 30000, GasCost = 10000, Depth = 1, Stack = new List<string>() }
            };

            var neth = new List<NethTraceStep>
            {
                new NethTraceStep { PC = 0, Op = "CALL", Gas = 30000, GasCost = 100, Depth = 0, Stack = new List<string>() }
            };

            var validator = new TraceValidator();
            var result = validator.Validate(geth, neth, new TraceValidationOptions { ValidateGasCost = true });

            Assert.True(result.IsValid, result.ToString());
        }

        [Fact]
        public void TraceValidator_Validate_DetectsStepCountMismatch()
        {
            var geth = new List<GethTraceStep>
            {
                new GethTraceStep { PC = 0, Op = "PUSH1", Gas = 30000, GasCost = 3, Depth = 1, Stack = new List<string>() },
                new GethTraceStep { PC = 2, Op = "PUSH1", Gas = 29997, GasCost = 3, Depth = 1, Stack = new List<string>() }
            };

            var neth = new List<NethTraceStep>
            {
                new NethTraceStep { PC = 0, Op = "PUSH1", Gas = 30000, GasCost = 3, Depth = 0, Stack = new List<string>() }
            };

            var validator = new TraceValidator();
            var result = validator.Validate(geth, neth);

            Assert.False(result.IsValid);
            Assert.Equal("NETH_ENDED", result.FirstMismatch.Field);
        }

        [Fact]
        public void TraceValidationResult_ToString_FormatsCorrectly()
        {
            var result = new TraceValidationResult
            {
                IsValid = false,
                TotalGethSteps = 100,
                TotalNethSteps = 99,
                MatchedSteps = 50,
                FirstMismatch = new StepMismatch
                {
                    StepIndex = 50,
                    Field = "PC",
                    GethValue = "123",
                    NethValue = "456",
                    GethStep = new GethTraceStep { PC = 123, Op = "SLOAD", Gas = 1000, GasCost = 100, Depth = 1 },
                    NethStep = new NethTraceStep { PC = 456, Op = "SLOAD", Gas = 1000, GasCost = 100, Depth = 0 }
                }
            };

            var str = result.ToString();

            Assert.Contains("INVALID", str);
            Assert.Contains("50", str);
            Assert.Contains("PC", str);
            Assert.Contains("123", str);
            Assert.Contains("456", str);
        }

        [Fact(Skip = "Run manually - requires geth-tools/evm.exe and test vectors")]
        public async Task GethEvmRunner_RunStateTestAsync_ExecutesSuccessfully()
        {
            var testDir = FindTestVectorsDirectory();
            if (testDir == null)
            {
                _output.WriteLine("Test vectors directory not found");
                return;
            }

            var testFile = Path.Combine(testDir, "stExample", "add11.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                return;
            }

            var gethRunner = new GethEvmRunner();
            var result = await gethRunner.RunStateTestAsync(testFile);

            _output.WriteLine($"Success: {result.Success}");
            _output.WriteLine($"Exit code: {result.ExitCode}");
            _output.WriteLine($"Steps captured: {result.Steps?.Count ?? 0}");
            _output.WriteLine($"Error: {result.Error}");

            if (result.Steps != null && result.Steps.Count > 0)
            {
                _output.WriteLine("\nFirst 10 geth steps:");
                foreach (var step in result.Steps.Take(10))
                {
                    _output.WriteLine($"  PC={step.PC} Op={step.Op} Gas={step.Gas} Cost={step.GasCost} Depth={step.Depth}");
                }
            }

            Assert.True(result.Success, result.Error);
            Assert.True(result.Steps.Count > 0, "No steps captured from geth");
        }

        [Fact(Skip = "Run manually - requires geth-tools/evm.exe and test vectors")]
        public async Task RunAndValidate_SimpleTest()
        {
            var testDir = FindTestVectorsDirectory();
            if (testDir == null)
            {
                _output.WriteLine("Test vectors directory not found");
                return;
            }

            var testFile = Path.Combine(testDir, "stExample", "add11.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                return;
            }

            var result = await _runner.RunAndValidateAsync(testFile);

            _output.WriteLine($"Validation result: {result}");
            _output.WriteLine($"Geth steps: {result.TotalGethSteps}");
            _output.WriteLine($"Neth steps: {result.TotalNethSteps}");
            _output.WriteLine($"Matched: {result.MatchedSteps}");

            Assert.True(result.IsValid, result.ToString());
        }

        [Fact(Skip = "Run manually - requires geth-tools/evm.exe and test vectors")]
        public async Task RunAndValidateFull_WithDiagnostics()
        {
            var testDir = FindTestVectorsDirectory();
            if (testDir == null)
            {
                _output.WriteLine("Test vectors directory not found");
                return;
            }

            // Default to simple test
            var testFile = Path.Combine(testDir, "stExample", "add11.json");
            if (!File.Exists(testFile))
            {
                testFile = Directory.GetFiles(testDir, "*.json", SearchOption.AllDirectories).FirstOrDefault();
            }

            if (testFile == null)
            {
                _output.WriteLine("No test file found");
                return;
            }

            _output.WriteLine($"Running: {testFile}");

            var (geth, neth, validation) = await _runner.RunAndValidateFullAsync(testFile, new TraceValidationOptions
            {
                ValidateGasCost = true,
                ValidateStack = false,
                ValidateMemory = false,
                ValidateStorage = false,
                ContinueOnMismatch = false
            });

            _output.WriteLine($"\n=== GETH ===");
            _output.WriteLine($"Success: {geth.Success}");
            _output.WriteLine($"Steps: {geth.Steps?.Count ?? 0}");
            if (!string.IsNullOrEmpty(geth.Error))
                _output.WriteLine($"Error: {geth.Error}");

            _output.WriteLine($"\n=== NETHEREUM ===");
            var nethSingle = neth.Results.FirstOrDefault(r => !r.Skipped);
            _output.WriteLine($"Passed: {nethSingle?.Passed}");
            _output.WriteLine($"Traces: {nethSingle?.Traces?.Count ?? 0}");

            _output.WriteLine($"\n=== VALIDATION ===");
            _output.WriteLine($"Valid: {validation.IsValid}");
            _output.WriteLine($"Matched: {validation.MatchedSteps}/{validation.TotalGethSteps}");

            if (!validation.IsValid)
            {
                _output.WriteLine($"\nFirst Mismatch:");
                _output.WriteLine($"  Step: {validation.FirstMismatch.StepIndex}");
                _output.WriteLine($"  Field: {validation.FirstMismatch.Field}");
                _output.WriteLine($"  Geth: {validation.FirstMismatch.GethValue}");
                _output.WriteLine($"  Neth: {validation.FirstMismatch.NethValue}");

                if (validation.FirstMismatch.GethStep != null)
                {
                    var g = validation.FirstMismatch.GethStep;
                    _output.WriteLine($"\n  Geth step context:");
                    _output.WriteLine($"    PC={g.PC} Op={g.Op} Gas={g.Gas} Cost={g.GasCost} Depth={g.Depth}");
                    if (g.Stack?.Count > 0)
                        _output.WriteLine($"    Stack[top]: {g.Stack.LastOrDefault()}");
                }

                if (validation.FirstMismatch.NethStep != null)
                {
                    var n = validation.FirstMismatch.NethStep;
                    _output.WriteLine($"\n  Neth step context:");
                    _output.WriteLine($"    PC={n.PC} Op={n.Op} Gas={n.Gas} Cost={n.GasCost} Depth={n.Depth}");
                    if (n.Stack?.Count > 0)
                        _output.WriteLine($"    Stack[top]: {n.Stack.LastOrDefault()}");
                }
            }

            Assert.True(validation.IsValid, validation.ToString());
        }

        [Fact(Skip = "Run manually - requires geth-tools/evm.exe and test vectors")]
        public async Task ValidateCategory_stExample()
        {
            var testDir = FindTestVectorsDirectory();
            if (testDir == null)
            {
                _output.WriteLine("Test vectors directory not found");
                return;
            }

            var categoryPath = Path.Combine(testDir, "stExample");
            if (!Directory.Exists(categoryPath))
            {
                _output.WriteLine($"Category not found: {categoryPath}");
                return;
            }

            var testFiles = Directory.GetFiles(categoryPath, "*.json", SearchOption.AllDirectories);
            var passed = 0;
            var failed = 0;
            var failedTests = new List<(string file, string reason)>();

            foreach (var testFile in testFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(testFile);
                try
                {
                    var result = await _runner.RunAndValidateAsync(testFile);

                    if (result.IsValid)
                    {
                        passed++;
                        _output.WriteLine($"PASS: {fileName} ({result.MatchedSteps} steps)");
                    }
                    else
                    {
                        failed++;
                        var reason = $"{result.FirstMismatch.Field} at step {result.FirstMismatch.StepIndex}";
                        failedTests.Add((fileName, reason));
                        _output.WriteLine($"FAIL: {fileName} - {reason}");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    failedTests.Add((fileName, ex.Message));
                    _output.WriteLine($"ERROR: {fileName} - {ex.Message}");
                }
            }

            _output.WriteLine($"\n=== SUMMARY ===");
            _output.WriteLine($"Passed: {passed}");
            _output.WriteLine($"Failed: {failed}");

            if (failedTests.Count > 0)
            {
                _output.WriteLine("\nFailed tests:");
                foreach (var (file, reason) in failedTests)
                {
                    _output.WriteLine($"  {file}: {reason}");
                }
            }

            Assert.Equal(0, failed);
        }
    }
}
