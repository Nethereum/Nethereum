using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.EVM.SourceInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.EVM.Debugging
{
    public class EVMDebuggerSession
    {
        private readonly IABIInfoStorage _abiStorage;
        private Dictionary<string, ABIInfo> _loadedContracts = new Dictionary<string, ABIInfo>();
        private Dictionary<string, List<SourceMap>> _contractSourceMaps = new Dictionary<string, List<SourceMap>>();
        private Dictionary<string, Dictionary<int, int>> _pcToInstructionIndex = new Dictionary<string, Dictionary<int, int>>();
        private Dictionary<string, Dictionary<int, string>> _functionMaps = new Dictionary<string, Dictionary<int, string>>();
        private Dictionary<int, string> _functionNameCache = new Dictionary<int, string>();
        private HashSet<string> _checkedAddresses = new HashSet<string>();
        private BigInteger _chainId;

        public List<ProgramTrace> Trace { get; private set; }
        public int CurrentStep { get; private set; }
        public bool CanStepForward => Trace != null && CurrentStep < Trace.Count - 1;
        public bool CanStepBack => Trace != null && CurrentStep > 0;
        public int TotalSteps => Trace?.Count ?? 0;

        public EVMDebuggerSession(IABIInfoStorage abiStorage)
        {
            _abiStorage = abiStorage ?? throw new ArgumentNullException(nameof(abiStorage));
        }

        public void SetContractDebugInfo(string address, ABIInfo abiInfo)
        {
            if (abiInfo == null) return;
            var normalizedAddress = address?.ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedAddress)) return;

            _loadedContracts[normalizedAddress] = abiInfo;
            _checkedAddresses.Add(normalizedAddress);

            if (!string.IsNullOrEmpty(abiInfo.RuntimeSourceMap))
            {
                var sourceMaps = new SourceMapUtil().UnCompressSourceMap(abiInfo.RuntimeSourceMap);
                _contractSourceMaps[normalizedAddress] = sourceMaps;
            }

            BuildPcToInstructionIndex(normalizedAddress, abiInfo);
        }

        public void LoadFromProgram(Program executedProgram, BigInteger chainId)
        {
            if (executedProgram == null) throw new ArgumentNullException(nameof(executedProgram));

            Trace = executedProgram.Trace;
            CurrentStep = 0;
            _chainId = chainId;
            _loadedContracts.Clear();
            _contractSourceMaps.Clear();
            _pcToInstructionIndex.Clear();
            _functionMaps.Clear();
            _functionNameCache.Clear();
            _checkedAddresses.Clear();

            var addresses = Trace.Select(t => t.CodeAddress).Where(a => !string.IsNullOrEmpty(a)).Distinct();
            foreach (var addr in addresses)
            {
                LoadContractDebugInfo(addr);
            }
        }

        public async Task LoadFromProgramAsync(Program executedProgram, BigInteger chainId)
        {
            if (executedProgram == null) throw new ArgumentNullException(nameof(executedProgram));

            Trace = executedProgram.Trace;
            CurrentStep = 0;
            _chainId = chainId;
            _loadedContracts.Clear();
            _contractSourceMaps.Clear();
            _pcToInstructionIndex.Clear();
            _functionNameCache.Clear();
            _checkedAddresses.Clear();

            var addresses = Trace.Select(t => t.CodeAddress).Where(a => !string.IsNullOrEmpty(a)).Distinct();
            foreach (var addr in addresses)
            {
                await LoadContractDebugInfoAsync(addr);
            }
        }

        public void LoadFromTrace(List<ProgramTrace> trace, BigInteger chainId)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));

            Trace = trace;
            CurrentStep = 0;
            _chainId = chainId;
            _loadedContracts.Clear();
            _contractSourceMaps.Clear();
            _pcToInstructionIndex.Clear();
            _functionNameCache.Clear();
            _checkedAddresses.Clear();

            var addresses = Trace.Select(t => t.CodeAddress).Where(a => !string.IsNullOrEmpty(a)).Distinct();
            foreach (var addr in addresses)
            {
                LoadContractDebugInfo(addr);
            }
        }

        public async Task LoadFromTraceAsync(List<ProgramTrace> trace, BigInteger chainId)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));

            Trace = trace;
            CurrentStep = 0;
            _chainId = chainId;
            _loadedContracts.Clear();
            _contractSourceMaps.Clear();
            _pcToInstructionIndex.Clear();
            _functionNameCache.Clear();
            _checkedAddresses.Clear();

            var addresses = Trace.Select(t => t.CodeAddress).Where(a => !string.IsNullOrEmpty(a)).Distinct();
            foreach (var addr in addresses)
            {
                await LoadContractDebugInfoAsync(addr);
            }
        }

        private void BuildPcToInstructionIndex(string normalizedAddress, ABIInfo abiInfo)
        {
            if (string.IsNullOrEmpty(abiInfo?.RuntimeBytecode))
                return;

            var bytecodeHex = abiInfo.RuntimeBytecode;
            if (bytecodeHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                bytecodeHex = bytecodeHex.Substring(2);

            byte[] bytecodeBytes;
            try
            {
                bytecodeBytes = Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(bytecodeHex);
            }
            catch
            {
                return;
            }

            var instructions = ProgramInstructionsUtils.GetProgramInstructions(bytecodeBytes);
            var pcMap = new Dictionary<int, int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                pcMap[instructions[i].Step] = i;
            }
            _pcToInstructionIndex[normalizedAddress] = pcMap;

            abiInfo.InitialiseContractABI();
            var funcMap = ProgramInstructionsUtils.GetFunctionDispatcherMap(instructions, abiInfo.ContractABI);
            if (funcMap.Count > 0)
                _functionMaps[normalizedAddress] = funcMap;
        }

        private void LoadContractDebugInfo(string address)
        {
            var normalizedAddress = address?.ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedAddress) || _checkedAddresses.Contains(normalizedAddress))
                return;

            _checkedAddresses.Add(normalizedAddress);
            var abiInfo = _abiStorage.GetABIInfo(_chainId, normalizedAddress);
            if (abiInfo != null)
            {
                _loadedContracts[normalizedAddress] = abiInfo;

                if (!string.IsNullOrEmpty(abiInfo.RuntimeSourceMap))
                {
                    var sourceMaps = new SourceMapUtil().UnCompressSourceMap(abiInfo.RuntimeSourceMap);
                    _contractSourceMaps[normalizedAddress] = sourceMaps;
                }

                BuildPcToInstructionIndex(normalizedAddress, abiInfo);
            }
        }

        private async Task LoadContractDebugInfoAsync(string address)
        {
            var normalizedAddress = address?.ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedAddress) || _checkedAddresses.Contains(normalizedAddress))
                return;

            _checkedAddresses.Add(normalizedAddress);
            var abiInfo = await _abiStorage.GetABIInfoAsync((long)_chainId, normalizedAddress);
            if (abiInfo != null)
            {
                _loadedContracts[normalizedAddress] = abiInfo;

                if (!string.IsNullOrEmpty(abiInfo.RuntimeSourceMap))
                {
                    var sourceMaps = new SourceMapUtil().UnCompressSourceMap(abiInfo.RuntimeSourceMap);
                    _contractSourceMaps[normalizedAddress] = sourceMaps;
                }

                BuildPcToInstructionIndex(normalizedAddress, abiInfo);
            }
        }

        public void StepForward()
        {
            if (CanStepForward) CurrentStep++;
        }

        public void StepBack()
        {
            if (CanStepBack) CurrentStep--;
        }

        public void GoToStep(int step)
        {
            if (Trace == null || Trace.Count == 0) return;
            CurrentStep = Math.Max(0, Math.Min(step, Trace.Count - 1));
        }

        public void GoToStart()
        {
            CurrentStep = 0;
        }

        public void GoToEnd()
        {
            if (Trace != null && Trace.Count > 0)
                CurrentStep = Trace.Count - 1;
        }

        public ProgramTrace CurrentTrace => Trace != null && CurrentStep >= 0 && CurrentStep < Trace.Count
            ? Trace[CurrentStep]
            : null;

        public ProgramInstruction CurrentInstruction => CurrentTrace?.Instruction;

        public List<string> CurrentStack => CurrentTrace?.Stack;

        public string CurrentMemory => CurrentTrace?.Memory;

        public Dictionary<string, string> CurrentStorage => CurrentTrace?.Storage;

        public int CurrentDepth => CurrentTrace?.Depth ?? 0;

        public BigInteger CurrentGasCost => CurrentTrace?.GasCost ?? 0;

        public string CurrentCodeAddress => CurrentTrace?.CodeAddress;

        public string CurrentProgramAddress => CurrentTrace?.ProgramAddress;

        public string GetContractNameForAddress(string address)
        {
            var abiInfo = GetABIInfoForAddress(address);
            return abiInfo?.ContractName;
        }

        public string GetFunctionNameForStep(int stepIndex)
        {
            if (Trace == null || stepIndex < 0 || stepIndex >= Trace.Count)
                return null;

            if (_functionNameCache.TryGetValue(stepIndex, out var cached))
                return cached;

            var trace = Trace[stepIndex];
            var addr = trace.CodeAddress?.ToLowerInvariant();
            if (addr == null || !_functionMaps.TryGetValue(addr, out var funcMap))
            {
                _functionNameCache[stepIndex] = null;
                return null;
            }

            var depth = trace.Depth;
            for (int i = stepIndex; i >= 0; i--)
            {
                var t = Trace[i];
                if (t.CodeAddress?.ToLowerInvariant() != addr) break;
                if (t.Depth != depth) break;

                var pc = t.Instruction?.Step ?? -1;
                if (funcMap.TryGetValue(pc, out var name))
                {
                    _functionNameCache[stepIndex] = name;
                    return name;
                }
            }

            _functionNameCache[stepIndex] = null;
            return null;
        }

        public string GetCurrentContractName()
        {
            return GetContractNameForAddress(CurrentCodeAddress);
        }

        public CallStepInfo GetCallInfoForStep(int stepIndex)
        {
            if (Trace == null || stepIndex < 0 || stepIndex >= Trace.Count)
                return null;

            var trace = Trace[stepIndex];
            var opcode = trace.Instruction?.Instruction;

            if (opcode != Instruction.CALL && opcode != Instruction.STATICCALL &&
                opcode != Instruction.DELEGATECALL && opcode != Instruction.CALLCODE)
                return null;

            if (trace.Stack == null || trace.Stack.Count < 4)
                return null;

            int toIndex = 1;
            int argsOffsetIndex = opcode == Instruction.CALL ? 3 : 2;
            int argsLengthIndex = opcode == Instruction.CALL ? 4 : 3;

            if (trace.Stack.Count <= argsLengthIndex)
                return null;

            var rawTo = trace.Stack[toIndex].PadLeft(40, '0');
            var targetAddress = "0x" + rawTo.Substring(rawTo.Length - 40);

            var result = new CallStepInfo
            {
                TargetAddress = targetAddress,
                ContractName = GetContractNameForAddress(targetAddress),
                CallType = opcode.Value.ToString()
            };

            if (!string.IsNullOrEmpty(trace.Memory))
            {
                try
                {
                    var argsOffset = ParseHexToInt(trace.Stack[argsOffsetIndex]);
                    var argsLength = ParseHexToInt(trace.Stack[argsLengthIndex]);

                    if (argsLength >= 4 && argsOffset * 2 + argsLength * 2 <= trace.Memory.Length)
                    {
                        var calldata = trace.Memory.Substring(argsOffset * 2, argsLength * 2);
                        result.Selector = "0x" + calldata.Substring(0, 8);
                        result.RawCalldata = calldata;

                        var abiInfo = GetABIInfoForAddress(targetAddress);
                        if (abiInfo != null)
                        {
                            abiInfo.InitialiseContractABI();
                            if (abiInfo.ContractABI != null)
                            {
                                var funcAbi = abiInfo.ContractABI.FindFunctionABIFromInputData("0x" + calldata);
                                if (funcAbi != null)
                                {
                                    result.FunctionName = funcAbi.Name;
                                    result.FunctionSignature = funcAbi.Signature;

                                    if (funcAbi.InputParameters != null && funcAbi.InputParameters.Length > 0)
                                    {
                                        try
                                        {
                                            var decoder = new FunctionCallDecoder();
                                            result.DecodedInputs = decoder.DecodeInput(funcAbi, "0x" + calldata);
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            return result;
        }

        private static int ParseHexToInt(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return 0;
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);
            if (hex.Length > 8)
                hex = hex.Substring(hex.Length - 8);
            return int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        }

        public ABIInfo GetABIInfoForAddress(string address)
        {
            var normalizedAddress = address?.ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedAddress))
                return null;

            if (!_loadedContracts.TryGetValue(normalizedAddress, out var abiInfo))
            {
                if (_checkedAddresses.Contains(normalizedAddress))
                    return null;

                LoadContractDebugInfo(normalizedAddress);
                _loadedContracts.TryGetValue(normalizedAddress, out abiInfo);
            }

            return abiInfo;
        }

        public SourceLocation GetCurrentSourceLocation()
        {
            return GetSourceLocationForStep(CurrentStep);
        }

        public SourceLocation GetSourceLocationForStep(int stepIndex)
        {
            if (Trace == null || stepIndex < 0 || stepIndex >= Trace.Count)
                return null;

            var trace = Trace[stepIndex];
            var abiInfo = GetABIInfoForAddress(trace.CodeAddress);
            if (abiInfo == null || !abiInfo.HasDebugInfo)
                return null;

            var normalizedAddress = trace.CodeAddress?.ToLowerInvariant();
            if (!_contractSourceMaps.TryGetValue(normalizedAddress, out var sourceMaps))
                return null;

            var instructionIndex = GetInstructionIndex(trace);
            if (instructionIndex < 0 || instructionIndex >= sourceMaps.Count)
                return null;

            var sourceMap = sourceMaps[instructionIndex];
            if (sourceMap.SourceFile < 0)
                return null;

            var filePath = abiInfo.GetSourceFilePath(sourceMap.SourceFile);
            if (string.IsNullOrEmpty(filePath))
                return null;

            var fileContent = abiInfo.GetSourceContent(sourceMap.SourceFile);
            if (string.IsNullOrEmpty(fileContent))
                return null;

            return SourceLocation.FromSourceMap(sourceMap, filePath, fileContent);
        }

        public SourceLocation GetNearestSourceLocation(int stepIndex, int maxLookahead = 20)
        {
            var location = GetSourceLocationForStep(stepIndex);
            if (location != null) return location;

            if (Trace == null || stepIndex < 0 || stepIndex >= Trace.Count)
                return null;

            var trace = Trace[stepIndex];

            var functionName = GetFunctionNameForStep(stepIndex);
            if (!string.IsNullOrEmpty(functionName))
            {
                var funcDeclLocation = GetFunctionDeclarationLocation(functionName, trace.CodeAddress);
                if (funcDeclLocation != null)
                    return funcDeclLocation;
            }

            var addr = trace.CodeAddress?.ToLowerInvariant();
            var depth = trace.Depth;

            for (int i = stepIndex + 1; i < Trace.Count && i <= stepIndex + maxLookahead; i++)
            {
                var t = Trace[i];
                if (t.CodeAddress?.ToLowerInvariant() != addr || t.Depth != depth)
                    break;
                var loc = GetSourceLocationForStep(i);
                if (loc != null) return loc;
            }

            return null;
        }

        public SourceLocation GetFunctionDeclarationLocation(string functionName, string codeAddress)
        {
            if (string.IsNullOrEmpty(functionName) || functionName.StartsWith("0x"))
                return null;

            var contractName = GetContractNameForAddress(codeAddress);

            foreach (var kvp in _loadedContracts)
            {
                var abiInfo = kvp.Value;
                if (abiInfo?.SourceFileIndex == null || abiInfo.Metadata?.Sources == null)
                    continue;

                if (!string.IsNullOrEmpty(contractName) &&
                    !string.IsNullOrEmpty(abiInfo.ContractName) &&
                    !string.Equals(abiInfo.ContractName, contractName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var referencedIndices = GetReferencedSourceFileIndices(kvp.Key);
                foreach (var fileEntry in abiInfo.SourceFileIndex)
                {
                    if (referencedIndices.Count > 0 && !referencedIndices.Contains(fileEntry.Key))
                        continue;

                    var fileContent = abiInfo.GetSourceContent(fileEntry.Key);
                    if (string.IsNullOrEmpty(fileContent))
                        continue;

                    var loc = BuildFunctionLocation(fileEntry.Value, fileContent, fileEntry.Key, functionName);
                    if (loc != null) return loc;
                }
            }

            if (!string.IsNullOrEmpty(contractName))
            {
                foreach (var kvp in _loadedContracts)
                {
                    var abiInfo = kvp.Value;
                    if (abiInfo?.SourceFileIndex == null || abiInfo.Metadata?.Sources == null)
                        continue;
                    if (string.Equals(abiInfo.ContractName, contractName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (var fileEntry in abiInfo.SourceFileIndex)
                    {
                        var fileContent = abiInfo.GetSourceContent(fileEntry.Key);
                        if (string.IsNullOrEmpty(fileContent))
                            continue;

                        var loc = BuildFunctionLocation(fileEntry.Value, fileContent, fileEntry.Key, functionName);
                        if (loc != null) return loc;
                    }
                }
            }

            return null;
        }

        private static SourceLocation BuildFunctionLocation(string filePath, string fileContent, int fileIndex, string functionName)
        {
            var lineNumber = FindFunctionDeclarationLine(fileContent, functionName);
            if (lineNumber <= 0) return null;

            var lines = fileContent.Split('\n');
            var position = 0;
            for (int i = 0; i < lineNumber - 1 && i < lines.Length; i++)
                position += lines[i].Length + 1;

            return new SourceLocation
            {
                FilePath = filePath,
                Position = position,
                Length = lines[lineNumber - 1].TrimEnd('\r').Length,
                SourceCode = lines[lineNumber - 1].TrimEnd('\r').Trim(),
                FullFileContent = fileContent,
                LineNumber = lineNumber,
                ColumnNumber = 1,
                SourceFileIndex = fileIndex
            };
        }

        private static int FindFunctionDeclarationLine(string content, string functionName)
        {
            var lines = content.Split('\n');
            var pattern = "function " + functionName + "(";

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().Contains(pattern))
                    return i + 1;
            }

            var fallbackPattern = "function " + functionName;
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith(fallbackPattern))
                {
                    var afterName = trimmed.Substring(fallbackPattern.Length);
                    if (afterName.Length > 0 && (afterName[0] == '(' || char.IsWhiteSpace(afterName[0])))
                        return i + 1;
                }
            }

            return -1;
        }

        private int GetInstructionIndex(ProgramTrace trace)
        {
            if (trace?.Instruction == null) return -1;

            var addr = trace.CodeAddress?.ToLowerInvariant();
            if (addr != null && _pcToInstructionIndex.TryGetValue(addr, out var pcMap))
            {
                if (pcMap.TryGetValue(trace.Instruction.Step, out var idx))
                    return idx;
            }

            return trace.ProgramTraceStep;
        }

        public List<int> FindStepsForSourceLine(string filePath, int lineNumber)
        {
            var result = new List<int>();
            if (Trace == null || string.IsNullOrEmpty(filePath))
                return result;

            for (int step = 0; step < Trace.Count; step++)
            {
                var trace = Trace[step];
                var abiInfo = GetABIInfoForAddress(trace.CodeAddress);
                if (abiInfo == null || !abiInfo.HasDebugInfo)
                    continue;

                var normalizedAddress = trace.CodeAddress?.ToLowerInvariant();
                if (!_contractSourceMaps.TryGetValue(normalizedAddress, out var sourceMaps))
                    continue;

                var instructionIndex = GetInstructionIndex(trace);
                if (instructionIndex < 0 || instructionIndex >= sourceMaps.Count)
                    continue;

                var sourceMap = sourceMaps[instructionIndex];
                var mapFilePath = abiInfo.GetSourceFilePath(sourceMap.SourceFile);
                if (!string.Equals(mapFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileContent = abiInfo.GetSourceContent(sourceMap.SourceFile);
                if (string.IsNullOrEmpty(fileContent))
                    continue;

                var mapLineNumber = GetLineNumber(fileContent, sourceMap.Position);
                if (mapLineNumber == lineNumber)
                {
                    result.Add(step);
                }
            }

            return result;
        }

        private int GetLineNumber(string content, int position)
        {
            if (string.IsNullOrEmpty(content) || position < 0 || position >= content.Length)
                return 1;

            int line = 1;
            for (int i = 0; i < position && i < content.Length; i++)
            {
                if (content[i] == '\n')
                    line++;
            }

            return line;
        }

        private HashSet<int> GetReferencedSourceFileIndices(string normalizedAddress)
        {
            var indices = new HashSet<int>();
            if (!_contractSourceMaps.TryGetValue(normalizedAddress, out var sourceMaps))
                return indices;

            foreach (var sm in sourceMaps)
            {
                if (sm.SourceFile >= 0)
                    indices.Add(sm.SourceFile);
            }
            return indices;
        }

        public IEnumerable<string> GetSourceFiles()
        {
            var files = new HashSet<string>();

            foreach (var kvp in _loadedContracts)
            {
                var abiInfo = kvp.Value;
                if (abiInfo?.SourceFileIndex == null)
                    continue;

                var referencedIndices = GetReferencedSourceFileIndices(kvp.Key);
                foreach (var fileEntry in abiInfo.SourceFileIndex)
                {
                    if (referencedIndices.Contains(fileEntry.Key))
                        files.Add(fileEntry.Value);
                }
            }

            return files;
        }

        public Dictionary<string, string> GetAllSourceFileContents()
        {
            var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _loadedContracts)
            {
                var abiInfo = kvp.Value;
                if (abiInfo?.SourceFileIndex == null || abiInfo.Metadata?.Sources == null)
                    continue;

                var referencedIndices = GetReferencedSourceFileIndices(kvp.Key);
                foreach (var fileEntry in abiInfo.SourceFileIndex)
                {
                    if (!referencedIndices.Contains(fileEntry.Key))
                        continue;

                    var filePath = fileEntry.Value;
                    if (contents.ContainsKey(filePath))
                        continue;

                    var content = abiInfo.GetSourceContent(fileEntry.Key);
                    if (!string.IsNullOrEmpty(content))
                    {
                        contents[filePath] = content;
                    }
                }
            }

            return contents;
        }

        public string ToDebugString()
        {
            var trace = CurrentTrace;
            if (trace == null)
                return "No trace available";

            var sb = new StringBuilder();
            sb.AppendLine($"=== Step {CurrentStep + 1}/{TotalSteps} ===");
            sb.AppendLine($"Address: {trace.CodeAddress}");
            sb.AppendLine($"Depth: {trace.Depth}");
            sb.AppendLine($"Gas: {trace.GasCost}");

            if (trace.Instruction != null)
            {
                sb.AppendLine($"Instruction: {trace.Instruction.ToDisassemblyLine()}");
            }

            var sourceLocation = GetCurrentSourceLocation();
            if (sourceLocation != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Source: {sourceLocation}");
                sb.AppendLine(sourceLocation.GetContextLines());
            }

            if (trace.Stack != null && trace.Stack.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Stack:");
                for (int i = 0; i < Math.Min(trace.Stack.Count, 10); i++)
                {
                    sb.AppendLine($"  [{i}] {trace.Stack[i]}");
                }
                if (trace.Stack.Count > 10)
                    sb.AppendLine($"  ... ({trace.Stack.Count - 10} more)");
            }

            if (trace.Storage != null && trace.Storage.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Storage:");
                var count = 0;
                foreach (var kvp in trace.Storage)
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                    if (++count >= 10)
                    {
                        sb.AppendLine($"  ... ({trace.Storage.Count - 10} more)");
                        break;
                    }
                }
            }

            return sb.ToString();
        }

        public string ToSummaryString()
        {
            var trace = CurrentTrace;
            if (trace == null)
                return "No trace";

            var sourceLocation = GetCurrentSourceLocation();
            var sourcePart = sourceLocation != null
                ? $" | {sourceLocation}"
                : "";

            return $"[{CurrentStep + 1}/{TotalSteps}] {trace.Instruction?.Instruction?.ToString() ?? "???"}{sourcePart}";
        }
    }

    public class CallStepInfo
    {
        public string TargetAddress { get; set; }
        public string ContractName { get; set; }
        public string CallType { get; set; }
        public string Selector { get; set; }
        public string FunctionName { get; set; }
        public string FunctionSignature { get; set; }
        public List<ParameterOutput> DecodedInputs { get; set; }
        public string RawCalldata { get; set; }
    }
}
