using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class GethEvmRunner
    {
        private readonly string _evmExePath;
        private readonly int _timeoutMs;

        public GethEvmRunner(string projectRoot = null, int timeoutMs = 60000)
        {
            _evmExePath = FindEvmExe(projectRoot);
            _timeoutMs = timeoutMs;
        }

        private static string FindEvmExe(string projectRoot)
        {
            if (projectRoot == null)
            {
                projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            }

            if (projectRoot == null)
                throw new FileNotFoundException("Could not find project root (Nethereum.sln)");

            var gethToolsDir = Path.Combine(projectRoot, "geth-tools");
            if (!Directory.Exists(gethToolsDir))
                throw new DirectoryNotFoundException($"geth-tools directory not found at: {gethToolsDir}");

            var evmExePaths = Directory.GetFiles(gethToolsDir, "evm.exe", SearchOption.AllDirectories);
            if (evmExePaths.Length == 0)
                throw new FileNotFoundException($"evm.exe not found in: {gethToolsDir}");

            return evmExePaths[0];
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

        private static int FindFlatIndex(string testFilePath, string fork, int dataIndex, int gasIndex, int valueIndex)
        {
            try
            {
                var json = File.ReadAllText(testFilePath);
                var jObject = JObject.Parse(json);

                foreach (var testEntry in jObject.Properties())
                {
                    var post = testEntry.Value["post"] as JObject;
                    if (post == null) continue;

                    var forkEntries = post[fork] as JArray;
                    if (forkEntries == null) continue;

                    for (int i = 0; i < forkEntries.Count; i++)
                    {
                        var entry = forkEntries[i];
                        var indexes = entry["indexes"];
                        if (indexes == null) continue;

                        var d = indexes["data"]?.Value<int>() ?? 0;
                        var g = indexes["gas"]?.Value<int>() ?? 0;
                        var v = indexes["value"]?.Value<int>() ?? 0;

                        if (d == dataIndex && g == gasIndex && v == valueIndex)
                        {
                            return i;
                        }
                    }
                    break;
                }
            }
            catch
            {
            }
            return dataIndex;
        }

        public async Task<GethEvmResult> RunStateTestAsync(string testFilePath, int dataIndex = 0, int gasIndex = 0, int valueIndex = 0, string fork = "Prague")
        {
            if (!File.Exists(_evmExePath))
                throw new FileNotFoundException($"evm.exe not found at: {_evmExePath}");

            if (!File.Exists(testFilePath))
                throw new FileNotFoundException($"Test file not found: {testFilePath}");

            // Calculate the flat index by finding the matching entry in the post array
            var flatIndex = FindFlatIndex(testFilePath, fork, dataIndex, gasIndex, valueIndex);

            var args = $"--json --nomemory=false --noreturndata=false statetest --statetest.fork {fork} --statetest.index {flatIndex} \"{testFilePath}\"";

            var result = new GethEvmResult
            {
                TestFile = testFilePath,
                Steps = new List<GethTraceStep>(),
                RawOutput = "",
                RawError = ""
            };

            var psi = new ProcessStartInfo
            {
                FileName = _evmExePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(testFilePath)
            };

            using var process = new Process { StartInfo = psi };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    errorBuilder.AppendLine(e.Data);
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var completed = await Task.Run(() => process.WaitForExit(_timeoutMs));
                if (!completed)
                {
                    try { process.Kill(); } catch { }
                    result.Error = "Geth EVM process timed out";
                    return result;
                }

                result.ExitCode = process.ExitCode;
                result.RawOutput = outputBuilder.ToString();
                result.RawError = errorBuilder.ToString();

                var stderrLines = result.RawError.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                result.Steps = ParseGethTraceLines(stderrLines);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Success = false;
            }

            return result;
        }

        private List<GethTraceStep> ParseGethTraceLines(string[] lines)
        {
            var steps = new List<GethTraceStep>();
            var comparer = new TraceComparer();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!line.TrimStart().StartsWith("{")) continue;

                try
                {
                    var obj = JObject.Parse(line);

                    if (obj["pc"] == null) continue;

                    var step = new GethTraceStep
                    {
                        PC = obj["pc"]?.Value<int>() ?? 0,
                        Op = obj["opName"]?.Value<string>() ?? obj["op"]?.Value<string>() ?? "",
                        Gas = ParseHexOrLong(obj["gas"]),
                        GasCost = ParseHexOrLong(obj["gasCost"]),
                        Depth = obj["depth"]?.Value<int>() ?? 1,
                        Error = obj["error"]?.Value<string>(),
                        Stack = ParseStack(obj["stack"] as JArray),
                        Memory = ParseMemory(obj["memory"] as JArray),
                        Storage = ParseStorage(obj["storage"] as JObject)
                    };
                    steps.Add(step);
                }
                catch
                {
                    continue;
                }
            }

            return steps;
        }

        private List<string> ParseStack(JArray stackArray)
        {
            if (stackArray == null) return new List<string>();
            return stackArray.Select(s => NormalizeHex(s.Value<string>())).ToList();
        }

        private string ParseMemory(JArray memoryArray)
        {
            if (memoryArray == null || memoryArray.Count == 0) return "";
            return string.Join("", memoryArray.Select(m => (m.Value<string>() ?? "").Replace("0x", "")));
        }

        private Dictionary<string, string> ParseStorage(JObject storageObj)
        {
            if (storageObj == null) return new Dictionary<string, string>();
            var result = new Dictionary<string, string>();
            foreach (var prop in storageObj.Properties())
            {
                result[NormalizeHex(prop.Name)] = NormalizeHex(prop.Value.Value<string>());
            }
            return result;
        }

        private static long ParseHexOrLong(JToken token)
        {
            if (token == null) return 0;
            var val = token.Value<string>();
            if (string.IsNullOrEmpty(val)) return 0;
            if (val.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt64(val, 16);
            }
            return long.TryParse(val, out var result) ? result : token.Value<long>();
        }

        private static string NormalizeHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return "0";
            hex = hex.ToLowerInvariant();
            if (hex.StartsWith("0x")) hex = hex.Substring(2);
            hex = hex.TrimStart('0');
            return string.IsNullOrEmpty(hex) ? "0" : hex;
        }
    }

    public class GethEvmResult
    {
        public string TestFile { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public int ExitCode { get; set; }
        public string RawOutput { get; set; }
        public string RawError { get; set; }
        public List<GethTraceStep> Steps { get; set; }
    }

    public class TraceValidator
    {
        private readonly HashSet<string> _callOpcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CALL", "CALLCODE", "DELEGATECALL", "STATICCALL", "CREATE", "CREATE2"
        };

        public TraceValidationResult Validate(List<GethTraceStep> gethSteps, List<NethTraceStep> nethSteps, TraceValidationOptions options = null)
        {
            options = options ?? new TraceValidationOptions();

            var result = new TraceValidationResult
            {
                TotalGethSteps = gethSteps?.Count ?? 0,
                TotalNethSteps = nethSteps?.Count ?? 0,
                IsValid = true,
                Mismatches = new List<StepMismatch>()
            };

            if (gethSteps == null || nethSteps == null)
            {
                result.IsValid = false;
                result.FirstMismatch = new StepMismatch
                {
                    StepIndex = 0,
                    Field = "TRACE_NULL",
                    GethValue = gethSteps == null ? "null" : $"{gethSteps.Count} steps",
                    NethValue = nethSteps == null ? "null" : $"{nethSteps.Count} steps"
                };
                return result;
            }

            int maxSteps = Math.Max(gethSteps.Count, nethSteps.Count);

            for (int i = 0; i < maxSteps; i++)
            {
                var gethStep = i < gethSteps.Count ? gethSteps[i] : null;
                var nethStep = i < nethSteps.Count ? nethSteps[i] : null;

                var mismatch = ValidateStep(i, gethStep, nethStep, options);
                if (mismatch != null)
                {
                    result.Mismatches.Add(mismatch);
                    if (result.FirstMismatch == null)
                    {
                        result.FirstMismatch = mismatch;
                        result.IsValid = false;
                    }

                    if (!options.ContinueOnMismatch)
                        break;
                }
                else
                {
                    result.MatchedSteps++;
                }
            }

            return result;
        }

        private StepMismatch ValidateStep(int index, GethTraceStep geth, NethTraceStep neth, TraceValidationOptions options)
        {
            if (geth == null && neth == null)
                return null;

            if (geth == null)
            {
                return new StepMismatch
                {
                    StepIndex = index,
                    Field = "GETH_ENDED",
                    GethValue = "N/A",
                    NethValue = $"PC={neth.PC} Op={neth.Op}",
                    NethStep = neth
                };
            }

            if (neth == null)
            {
                return new StepMismatch
                {
                    StepIndex = index,
                    Field = "NETH_ENDED",
                    GethValue = $"PC={geth.PC} Op={geth.Op}",
                    NethValue = "N/A",
                    GethStep = geth
                };
            }

            // Depth comparison: Nethereum now starts at depth 1 (same as Geth)
            if (geth.Depth != neth.Depth)
            {
                return new StepMismatch
                {
                    StepIndex = index,
                    Field = "DEPTH",
                    GethValue = geth.Depth.ToString(),
                    NethValue = neth.Depth.ToString(),
                    GethStep = geth,
                    NethStep = neth
                };
            }

            if (geth.PC != neth.PC)
            {
                return new StepMismatch
                {
                    StepIndex = index,
                    Field = "PC",
                    GethValue = geth.PC.ToString(),
                    NethValue = neth.PC.ToString(),
                    GethStep = geth,
                    NethStep = neth
                };
            }

            if (!string.Equals(geth.Op, neth.Op, StringComparison.OrdinalIgnoreCase))
            {
                return new StepMismatch
                {
                    StepIndex = index,
                    Field = "OP",
                    GethValue = geth.Op,
                    NethValue = neth.Op,
                    GethStep = geth,
                    NethStep = neth
                };
            }

            if (options.ValidateGasCost && !_callOpcodes.Contains(geth.Op))
            {
                if (geth.GasCost != neth.GasCost)
                {
                    return new StepMismatch
                    {
                        StepIndex = index,
                        Field = "GAS_COST",
                        GethValue = geth.GasCost.ToString(),
                        NethValue = neth.GasCost.ToString(),
                        GethStep = geth,
                        NethStep = neth
                    };
                }
            }

            if (options.ValidateGasRemaining)
            {
                var gasDiff = Math.Abs(geth.Gas - neth.Gas);
                if (gasDiff > options.GasRemainingTolerance)
                {
                    return new StepMismatch
                    {
                        StepIndex = index,
                        Field = "GAS_REMAINING",
                        GethValue = $"{geth.Gas} (0x{geth.Gas:X})",
                        NethValue = $"{neth.Gas} (0x{neth.Gas:X}), diff={geth.Gas - neth.Gas}",
                        GethStep = geth,
                        NethStep = neth
                    };
                }
            }

            if (options.ValidateStack)
            {
                var stackMismatch = ValidateStack(index, geth, neth);
                if (stackMismatch != null)
                    return stackMismatch;
            }

            if (options.ValidateMemory)
            {
                var memoryMismatch = ValidateMemory(index, geth, neth);
                if (memoryMismatch != null)
                    return memoryMismatch;
            }

            if (options.ValidateStorage)
            {
                var storageMismatch = ValidateStorage(index, geth, neth);
                if (storageMismatch != null)
                    return storageMismatch;
            }

            return null;
        }

        private StepMismatch ValidateStack(int index, GethTraceStep geth, NethTraceStep neth)
        {
            var gethStack = geth.Stack ?? new List<string>();
            var nethStack = neth.Stack ?? new List<string>();

            if (gethStack.Count != nethStack.Count)
            {
                return new StepMismatch
                {
                    StepIndex = index,
                    Field = "STACK_SIZE",
                    GethValue = $"{gethStack.Count} items",
                    NethValue = $"{nethStack.Count} items",
                    GethStep = geth,
                    NethStep = neth
                };
            }

            for (int i = 0; i < gethStack.Count; i++)
            {
                var gethVal = NormalizeHex(gethStack[i]);
                var nethVal = NormalizeHex(nethStack[i]);

                if (gethVal != nethVal)
                {
                    return new StepMismatch
                    {
                        StepIndex = index,
                        Field = $"STACK[{i}]",
                        GethValue = gethVal,
                        NethValue = nethVal,
                        GethStep = geth,
                        NethStep = neth
                    };
                }
            }

            return null;
        }

        private StepMismatch ValidateMemory(int index, GethTraceStep geth, NethTraceStep neth)
        {
            var gethMem = NormalizeHex(geth.Memory ?? "");
            var nethMem = NormalizeHex(neth.Memory ?? "");

            gethMem = gethMem.TrimEnd('0');
            nethMem = nethMem.TrimEnd('0');

            if (gethMem != nethMem)
            {
                var maxLen = 64;
                return new StepMismatch
                {
                    StepIndex = index,
                    Field = "MEMORY",
                    GethValue = gethMem.Length > maxLen ? gethMem.Substring(0, maxLen) + "..." : gethMem,
                    NethValue = nethMem.Length > maxLen ? nethMem.Substring(0, maxLen) + "..." : nethMem,
                    GethStep = geth,
                    NethStep = neth
                };
            }

            return null;
        }

        private StepMismatch ValidateStorage(int index, GethTraceStep geth, NethTraceStep neth)
        {
            var gethStorage = geth.Storage ?? new Dictionary<string, string>();
            var nethStorage = neth.Storage ?? new Dictionary<string, string>();

            var allKeys = gethStorage.Keys.Union(nethStorage.Keys).Distinct();

            foreach (var key in allKeys)
            {
                var gethHas = gethStorage.TryGetValue(key, out var gethVal);
                var nethHas = nethStorage.TryGetValue(key, out var nethVal);

                gethVal = NormalizeHex(gethVal ?? "0");
                nethVal = NormalizeHex(nethVal ?? "0");

                if (gethVal != nethVal)
                {
                    return new StepMismatch
                    {
                        StepIndex = index,
                        Field = $"STORAGE[{key}]",
                        GethValue = gethHas ? gethVal : "(not present)",
                        NethValue = nethHas ? nethVal : "(not present)",
                        GethStep = geth,
                        NethStep = neth
                    };
                }
            }

            return null;
        }

        private static string NormalizeHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return "0";
            hex = hex.ToLowerInvariant();
            if (hex.StartsWith("0x")) hex = hex.Substring(2);
            hex = hex.TrimStart('0');
            return string.IsNullOrEmpty(hex) ? "0" : hex;
        }
    }

    public class TraceValidationOptions
    {
        public bool ValidateGasCost { get; set; } = true;
        public bool ValidateGasRemaining { get; set; } = false;
        public long GasRemainingTolerance { get; set; } = 0;
        public bool ValidateStack { get; set; } = false;
        public bool ValidateMemory { get; set; } = false;
        public bool ValidateStorage { get; set; } = false;
        public bool ContinueOnMismatch { get; set; } = false;
    }

    public class TraceValidationResult
    {
        public bool IsValid { get; set; }
        public int TotalGethSteps { get; set; }
        public int TotalNethSteps { get; set; }
        public int MatchedSteps { get; set; }
        public StepMismatch FirstMismatch { get; set; }
        public List<StepMismatch> Mismatches { get; set; }

        public override string ToString()
        {
            if (IsValid)
                return $"VALID: {MatchedSteps}/{TotalGethSteps} steps match";

            var sb = new StringBuilder();
            sb.AppendLine($"INVALID: First mismatch at step {FirstMismatch?.StepIndex}");
            sb.AppendLine($"  Field: {FirstMismatch?.Field}");
            sb.AppendLine($"  Geth:  {FirstMismatch?.GethValue}");
            sb.AppendLine($"  Neth:  {FirstMismatch?.NethValue}");

            if (FirstMismatch?.GethStep != null)
            {
                var g = FirstMismatch.GethStep;
                sb.AppendLine($"  Geth Context: PC={g.PC} Op={g.Op} Gas={g.Gas} Cost={g.GasCost} Depth={g.Depth}");
            }
            if (FirstMismatch?.NethStep != null)
            {
                var n = FirstMismatch.NethStep;
                sb.AppendLine($"  Neth Context: PC={n.PC} Op={n.Op} Gas={n.Gas} Cost={n.GasCost} Depth={n.Depth}");
            }

            return sb.ToString();
        }
    }

    public class StepMismatch
    {
        public int StepIndex { get; set; }
        public string Field { get; set; }
        public string GethValue { get; set; }
        public string NethValue { get; set; }
        public GethTraceStep GethStep { get; set; }
        public NethTraceStep NethStep { get; set; }

        public override string ToString()
        {
            return $"Step {StepIndex}: {Field} mismatch - Geth={GethValue}, Neth={NethValue}";
        }
    }

    public class TraceComparer
    {
        public List<GethTraceStep> ParseGethTrace(string jsonContent)
        {
            var steps = new List<GethTraceStep>();
            var array = JArray.Parse(jsonContent);

            foreach (var item in array)
            {
                var step = new GethTraceStep
                {
                    PC = item["pc"]?.Value<int>() ?? 0,
                    Op = item["op"]?.Value<string>() ?? "",
                    Gas = item["gas"]?.Value<long>() ?? 0,
                    GasCost = item["gasCost"]?.Value<long>() ?? 0,
                    Depth = item["depth"]?.Value<int>() ?? 1,
                    Error = item["error"]?.Value<string>(),
                    Stack = ParseStack(item["stack"] as JArray),
                    Memory = ParseMemory(item["memory"] as JArray),
                    Storage = ParseStorage(item["storage"] as JObject)
                };
                steps.Add(step);
            }

            return steps;
        }

        public List<GethTraceStep> ParseGethTraceLines(string[] lines)
        {
            var steps = new List<GethTraceStep>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var obj = JObject.Parse(line);
                    var step = new GethTraceStep
                    {
                        PC = obj["pc"]?.Value<int>() ?? 0,
                        Op = obj["op"]?.Value<string>() ?? "",
                        Gas = obj["gas"]?.Value<long>() ?? 0,
                        GasCost = obj["gasCost"]?.Value<long>() ?? 0,
                        Depth = obj["depth"]?.Value<int>() ?? 1,
                        Error = obj["error"]?.Value<string>(),
                        Stack = ParseStack(obj["stack"] as JArray),
                        Memory = ParseMemory(obj["memory"] as JArray),
                        Storage = ParseStorage(obj["storage"] as JObject)
                    };
                    steps.Add(step);
                }
                catch
                {
                    continue;
                }
            }

            return steps;
        }

        public List<NethTraceStep> NormalizeNethTrace(List<ProgramTrace> traces)
        {
            if (traces == null) return new List<NethTraceStep>();

            var steps = new List<NethTraceStep>();
            foreach (var trace in traces)
            {
                var step = new NethTraceStep
                {
                    PC = trace.Instruction?.Step ?? 0,
                    Op = trace.Instruction?.Instruction?.ToString() ?? "UNKNOWN",
                    Gas = (long)trace.GasRemaining,
                    GasCost = (long)trace.GasCost,
                    Depth = trace.Depth + 1, // Geth starts at depth 1, Nethereum at 0
                    Stack = trace.Stack?.ToList() ?? new List<string>(),
                    Memory = trace.Memory ?? "",
                    Storage = trace.Storage ?? new Dictionary<string, string>(),
                    VMTraceStep = trace.VMTraceStep,
                    ProgramAddress = trace.ProgramAddress,
                    CodeAddress = trace.CodeAddress
                };
                steps.Add(step);
            }

            return steps;
        }

        public ComparisonResult Compare(List<GethTraceStep> geth, List<NethTraceStep> neth)
        {
            var result = new ComparisonResult
            {
                GethStepCount = geth.Count,
                NethStepCount = neth.Count,
                Steps = new List<StepComparison>()
            };

            int maxSteps = Math.Max(geth.Count, neth.Count);
            bool foundDivergence = false;

            for (int i = 0; i < maxSteps; i++)
            {
                var gethStep = i < geth.Count ? geth[i] : null;
                var nethStep = i < neth.Count ? neth[i] : null;

                var comparison = new StepComparison
                {
                    Step = i + 1,
                    GethPC = gethStep?.PC ?? -1,
                    NethPC = nethStep?.PC ?? -1,
                    GethOp = gethStep?.Op ?? "N/A",
                    NethOp = nethStep?.Op ?? "N/A",
                    GethGas = gethStep?.Gas ?? -1,
                    NethGas = nethStep?.Gas ?? -1,
                    GethCost = gethStep?.GasCost ?? -1,
                    NethCost = nethStep?.GasCost ?? -1,
                    GethDepth = gethStep?.Depth ?? -1,
                    NethDepth = nethStep?.Depth ?? -1,
                    GethStackTop = gethStep?.Stack?.LastOrDefault() ?? "",
                    NethStackTop = nethStep?.Stack?.LastOrDefault() ?? ""
                };

                DetermineMatchStatus(comparison, gethStep, nethStep);

                if (!comparison.IsMatch && !foundDivergence)
                {
                    foundDivergence = true;
                    result.FirstDivergenceStep = i + 1;
                    result.DivergenceReason = comparison.DivergenceType;
                    result.DivergenceDetails = BuildDivergenceDetails(comparison, gethStep, nethStep);
                }

                result.Steps.Add(comparison);
            }

            if (!foundDivergence)
            {
                result.FirstDivergenceStep = -1;
                result.DivergenceReason = "NONE";
                result.DivergenceDetails = "Traces match completely.";
            }

            return result;
        }

        private void DetermineMatchStatus(StepComparison comparison, GethTraceStep gethStep, NethTraceStep nethStep)
        {
            if (gethStep == null && nethStep == null)
            {
                comparison.IsMatch = true;
                comparison.DivergenceType = "EMPTY";
                return;
            }

            if (gethStep == null)
            {
                comparison.IsMatch = false;
                comparison.DivergenceType = "GETH_ENDED";
                return;
            }

            if (nethStep == null)
            {
                comparison.IsMatch = false;
                comparison.DivergenceType = "NETH_ENDED";
                return;
            }

            var divergences = new List<string>();

            if (comparison.GethDepth != comparison.NethDepth)
                divergences.Add("DEPTH");

            if (comparison.GethPC != comparison.NethPC)
                divergences.Add("PC");

            if (!string.Equals(comparison.GethOp, comparison.NethOp, StringComparison.OrdinalIgnoreCase))
                divergences.Add("OP");

            if (comparison.GethGas != comparison.NethGas)
                divergences.Add("GAS");

            if (comparison.GethCost != comparison.NethCost)
                divergences.Add("COST");

            comparison.IsMatch = divergences.Count == 0;
            comparison.DivergenceType = divergences.Count > 0 ? string.Join("+", divergences) : "MATCH";
        }

        private string BuildDivergenceDetails(StepComparison comparison, GethTraceStep gethStep, NethTraceStep nethStep)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"First divergence at step {comparison.Step}:");
            sb.AppendLine($"  Type: {comparison.DivergenceType}");

            if (comparison.GethDepth != comparison.NethDepth)
                sb.AppendLine($"  Depth: Geth={comparison.GethDepth}, Neth={comparison.NethDepth}");

            if (comparison.GethPC != comparison.NethPC)
                sb.AppendLine($"  PC: Geth={comparison.GethPC}, Neth={comparison.NethPC}");

            if (!string.Equals(comparison.GethOp, comparison.NethOp, StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"  Op: Geth={comparison.GethOp}, Neth={comparison.NethOp}");

            if (comparison.GethGas != comparison.NethGas)
                sb.AppendLine($"  Gas: Geth={comparison.GethGas}, Neth={comparison.NethGas}, Diff={comparison.GethGas - comparison.NethGas}");

            if (comparison.GethCost != comparison.NethCost)
                sb.AppendLine($"  Cost: Geth={comparison.GethCost}, Neth={comparison.NethCost}");

            if (gethStep != null && nethStep != null)
            {
                sb.AppendLine($"  Stack size: Geth={gethStep.Stack?.Count ?? 0}, Neth={nethStep.Stack?.Count ?? 0}");
                if (!string.IsNullOrEmpty(gethStep.Error))
                    sb.AppendLine($"  Geth error: {gethStep.Error}");
            }

            return sb.ToString();
        }

        public string GenerateReport(ComparisonResult result, string testName, string testFilePath = null)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# EVM Trace Divergence Report");
            sb.AppendLine();
            sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"**Test Name:** {testName}");
            if (!string.IsNullOrEmpty(testFilePath))
                sb.AppendLine($"**Test File:** {testFilePath}");
            sb.AppendLine();

            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine($"- **Geth Steps:** {result.GethStepCount}");
            sb.AppendLine($"- **Nethereum Steps:** {result.NethStepCount}");
            sb.AppendLine($"- **First Divergence:** {(result.FirstDivergenceStep > 0 ? $"Step {result.FirstDivergenceStep}" : "None")}");
            sb.AppendLine($"- **Divergence Type:** {result.DivergenceReason}");
            sb.AppendLine();

            if (result.FirstDivergenceStep > 0)
            {
                sb.AppendLine("## Divergence Details");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(result.DivergenceDetails);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            sb.AppendLine("## Trace Comparison");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(GenerateTraceTable(result));
            sb.AppendLine("```");
            sb.AppendLine();

            if (result.FirstDivergenceStep > 0)
            {
                sb.AppendLine("## Analysis Questions");
                sb.AppendLine();
                sb.AppendLine("### 1. WHERE does divergence occur?");
                sb.AppendLine($"- Step: {result.FirstDivergenceStep}");
                var divergeStep = result.Steps.FirstOrDefault(s => s.Step == result.FirstDivergenceStep);
                if (divergeStep != null)
                {
                    sb.AppendLine($"- Depth: Geth={divergeStep.GethDepth}, Neth={divergeStep.NethDepth}");
                    sb.AppendLine($"- PC: Geth={divergeStep.GethPC}, Neth={divergeStep.NethPC}");
                    sb.AppendLine($"- Opcode: Geth={divergeStep.GethOp}, Neth={divergeStep.NethOp}");
                }
                sb.AppendLine();

                sb.AppendLine("### 2. WHAT is geth doing vs Nethereum?");
                sb.AppendLine("(Fill in after analysis)");
                sb.AppendLine();

                sb.AppendLine("### 3. WHY does Nethereum differ?");
                sb.AppendLine("(Fill in after identifying source code location)");
                sb.AppendLine();

                sb.AppendLine("### 4. WHICH file/line causes this?");
                sb.AppendLine("(Fill in after source code investigation)");
                sb.AppendLine();

                sb.AppendLine("### 5. FIX - What change aligns Nethereum with geth?");
                sb.AppendLine("(Fill in after understanding root cause)");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public string GenerateTraceTable(ComparisonResult result, int? contextAroundDivergence = null)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Step  | Depth | PC    | Op           | G.Gas      | N.Gas      | G.Cost | N.Cost | Status");
            sb.AppendLine("------|-------|-------|--------------|------------|------------|--------|--------|--------");

            IEnumerable<StepComparison> stepsToShow = result.Steps;

            if (contextAroundDivergence.HasValue && result.FirstDivergenceStep > 0)
            {
                int start = Math.Max(0, result.FirstDivergenceStep - contextAroundDivergence.Value - 1);
                int end = Math.Min(result.Steps.Count, result.FirstDivergenceStep + contextAroundDivergence.Value);
                stepsToShow = result.Steps.Skip(start).Take(end - start);

                if (start > 0)
                    sb.AppendLine($"... (skipped {start} steps) ...");
            }

            foreach (var step in stepsToShow)
            {
                string status = step.IsMatch ? "MATCH" : $"<<<{step.DivergenceType}>>>";

                sb.AppendLine(
                    $"{step.Step,5} | " +
                    $"{(step.GethDepth == step.NethDepth ? step.GethDepth.ToString() : $"{step.GethDepth}/{step.NethDepth}"),5} | " +
                    $"{(step.GethPC == step.NethPC ? step.GethPC.ToString() : $"{step.GethPC}/{step.NethPC}"),5} | " +
                    $"{(step.GethOp == step.NethOp ? step.GethOp : $"{step.GethOp}/{step.NethOp}"),-12} | " +
                    $"{step.GethGas,10} | " +
                    $"{step.NethGas,10} | " +
                    $"{step.GethCost,6} | " +
                    $"{step.NethCost,6} | " +
                    $"{status}");
            }

            if (contextAroundDivergence.HasValue && result.FirstDivergenceStep > 0)
            {
                int end = result.FirstDivergenceStep + contextAroundDivergence.Value;
                if (end < result.Steps.Count)
                    sb.AppendLine($"... ({result.Steps.Count - end} more steps) ...");
            }

            return sb.ToString();
        }

        public string GenerateCompactReport(ComparisonResult result, string testName, int contextLines = 10)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"## {testName}");
            sb.AppendLine();
            sb.AppendLine($"Steps: Geth={result.GethStepCount}, Neth={result.NethStepCount}");

            if (result.FirstDivergenceStep < 0)
            {
                sb.AppendLine("**Result: MATCH** - Traces are identical");
            }
            else
            {
                sb.AppendLine($"**Result: DIVERGE** at step {result.FirstDivergenceStep} ({result.DivergenceReason})");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(GenerateTraceTable(result, contextLines));
                sb.AppendLine("```");
            }

            return sb.ToString();
        }

        private List<string> ParseStack(JArray stackArray)
        {
            if (stackArray == null) return new List<string>();
            return stackArray.Select(s => s.Value<string>()?.ToLowerInvariant().TrimStart('0') ?? "0").ToList();
        }

        private string ParseMemory(JArray memoryArray)
        {
            if (memoryArray == null || memoryArray.Count == 0) return "";
            return string.Join("", memoryArray.Select(m => m.Value<string>() ?? ""));
        }

        private Dictionary<string, string> ParseStorage(JObject storageObj)
        {
            if (storageObj == null) return new Dictionary<string, string>();
            var result = new Dictionary<string, string>();
            foreach (var prop in storageObj.Properties())
            {
                result[prop.Name.ToLowerInvariant()] = prop.Value.Value<string>()?.ToLowerInvariant() ?? "";
            }
            return result;
        }
    }

    public class GethTraceStep
    {
        public int PC { get; set; }
        public string Op { get; set; }
        public long Gas { get; set; }
        public long GasCost { get; set; }
        public int Depth { get; set; }
        public string Error { get; set; }
        public List<string> Stack { get; set; }
        public string Memory { get; set; }
        public Dictionary<string, string> Storage { get; set; }
    }

    public class NethTraceStep
    {
        public int PC { get; set; }
        public string Op { get; set; }
        public long Gas { get; set; }
        public long GasCost { get; set; }
        public int Depth { get; set; }
        public List<string> Stack { get; set; }
        public string Memory { get; set; }
        public Dictionary<string, string> Storage { get; set; }
        public int VMTraceStep { get; set; }
        public string ProgramAddress { get; set; }
        public string CodeAddress { get; set; }
    }

    public class ComparisonResult
    {
        public int GethStepCount { get; set; }
        public int NethStepCount { get; set; }
        public int FirstDivergenceStep { get; set; }
        public string DivergenceReason { get; set; }
        public string DivergenceDetails { get; set; }
        public List<StepComparison> Steps { get; set; }

        public bool HasDivergence => FirstDivergenceStep > 0;
        public int MatchingSteps => Steps?.Count(s => s.IsMatch) ?? 0;
    }

    public class StepComparison
    {
        public int Step { get; set; }
        public int GethDepth { get; set; }
        public int NethDepth { get; set; }
        public int GethPC { get; set; }
        public int NethPC { get; set; }
        public string GethOp { get; set; }
        public string NethOp { get; set; }
        public long GethGas { get; set; }
        public long NethGas { get; set; }
        public long GethCost { get; set; }
        public long NethCost { get; set; }
        public string GethStackTop { get; set; }
        public string NethStackTop { get; set; }
        public bool IsMatch { get; set; }
        public string DivergenceType { get; set; }
    }
}
