using Nethereum.ABI.ABIRepository;
using Nethereum.EVM.SourceInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Nethereum.EVM.Debugging
{
    public class EVMDebuggerSession
    {
        private readonly IABIInfoStorage _abiStorage;
        private Dictionary<string, ABIInfo> _loadedContracts = new Dictionary<string, ABIInfo>();
        private Dictionary<string, List<SourceMap>> _contractSourceMaps = new Dictionary<string, List<SourceMap>>();
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

        public void LoadFromProgram(Program executedProgram, BigInteger chainId)
        {
            if (executedProgram == null) throw new ArgumentNullException(nameof(executedProgram));

            Trace = executedProgram.Trace;
            CurrentStep = 0;
            _chainId = chainId;
            _loadedContracts.Clear();
            _contractSourceMaps.Clear();

            var addresses = Trace.Select(t => t.CodeAddress).Where(a => !string.IsNullOrEmpty(a)).Distinct();
            foreach (var addr in addresses)
            {
                LoadContractDebugInfo(addr);
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

            var addresses = Trace.Select(t => t.CodeAddress).Where(a => !string.IsNullOrEmpty(a)).Distinct();
            foreach (var addr in addresses)
            {
                LoadContractDebugInfo(addr);
            }
        }

        private void LoadContractDebugInfo(string address)
        {
            var normalizedAddress = address?.ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedAddress) || _loadedContracts.ContainsKey(normalizedAddress))
                return;

            var abiInfo = _abiStorage.GetABIInfo(_chainId, normalizedAddress);
            if (abiInfo != null)
            {
                _loadedContracts[normalizedAddress] = abiInfo;

                if (!string.IsNullOrEmpty(abiInfo.RuntimeSourceMap))
                {
                    var sourceMaps = new SourceMapUtil().UnCompressSourceMap(abiInfo.RuntimeSourceMap);
                    _contractSourceMaps[normalizedAddress] = sourceMaps;
                }
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

        public ABIInfo GetABIInfoForAddress(string address)
        {
            var normalizedAddress = address?.ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedAddress))
                return null;

            if (!_loadedContracts.TryGetValue(normalizedAddress, out var abiInfo))
            {
                LoadContractDebugInfo(normalizedAddress);
                _loadedContracts.TryGetValue(normalizedAddress, out abiInfo);
            }

            return abiInfo;
        }

        public SourceLocation GetCurrentSourceLocation()
        {
            var trace = CurrentTrace;
            if (trace == null) return null;

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

        private int GetInstructionIndex(ProgramTrace trace)
        {
            if (trace?.Instruction == null) return -1;
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

        public IEnumerable<string> GetSourceFiles()
        {
            var files = new HashSet<string>();

            foreach (var kvp in _loadedContracts)
            {
                var abiInfo = kvp.Value;
                if (abiInfo?.SourceFileIndex != null)
                {
                    foreach (var filePath in abiInfo.SourceFileIndex.Values)
                    {
                        files.Add(filePath);
                    }
                }
            }

            return files;
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
}
