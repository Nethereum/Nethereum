using Nethereum.ABI.ABIRepository;
using Nethereum.EVM.SourceInfo;
using System;
using System.Collections.Generic;

namespace Nethereum.EVM.Debugging
{
    public partial class EVMDebuggerSession
    {
        public SourceLocation GetCurrentSourceLocation()
        {
            return GetSourceLocationForStep(CurrentStep);
        }

        public SourceLocation GetSourceLocationForStep(int stepIndex)
        {
            if (!IsWithinTrace(stepIndex))
                return null;

            if (!TryResolveSourceMapForStep(Trace[stepIndex], out var abiInfo, out var sourceMap))
                return null;

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

            if (!IsWithinTrace(stepIndex))
                return null;

            return GetDeclarationLocationOfEnclosingFunction(stepIndex)
                ?? FindSourceLocationInFollowingSteps(stepIndex, maxLookahead);
        }

        public SourceLocation GetFunctionDeclarationLocation(string functionName, string codeAddress)
        {
            if (string.IsNullOrEmpty(functionName) || functionName.StartsWith("0x"))
                return null;

            var contractName = GetContractNameForAddress(codeAddress);

            var location = FindDeclarationInSourcesOfMatchingContracts(functionName, contractName);
            if (location != null) return location;

            if (string.IsNullOrEmpty(contractName)) return null;

            return FindDeclarationInSourcesOfOtherContracts(functionName, contractName);
        }

        public List<int> FindStepsForSourceLine(string filePath, int lineNumber)
        {
            var result = new List<int>();
            if (Trace == null || string.IsNullOrEmpty(filePath))
                return result;

            for (int step = 0; step < Trace.Count; step++)
            {
                if (StepMapsToSourceLine(Trace[step], filePath, lineNumber))
                    result.Add(step);
            }

            return result;
        }

        private bool TryResolveSourceMapForStep(ProgramTrace trace, out ABIInfo abiInfo, out SourceMap sourceMap)
        {
            sourceMap = null;

            abiInfo = GetABIInfoForAddress(trace.CodeAddress);
            if (abiInfo == null || !abiInfo.HasDebugInfo)
                return false;

            var normalizedAddress = trace.CodeAddress?.ToLowerInvariant();
            if (!_contractSourceMaps.TryGetValue(normalizedAddress, out var sourceMaps))
                return false;

            var instructionIndex = GetInstructionIndex(trace);
            if (instructionIndex < 0 || instructionIndex >= sourceMaps.Count)
                return false;

            sourceMap = sourceMaps[instructionIndex];
            return true;
        }

        private bool StepMapsToSourceLine(ProgramTrace trace, string filePath, int lineNumber)
        {
            if (!TryResolveSourceMapForStep(trace, out var abiInfo, out var sourceMap))
                return false;

            var mapFilePath = abiInfo.GetSourceFilePath(sourceMap.SourceFile);
            if (!string.Equals(mapFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                return false;

            var fileContent = abiInfo.GetSourceContent(sourceMap.SourceFile);
            if (string.IsNullOrEmpty(fileContent))
                return false;

            return GetLineNumber(fileContent, sourceMap.Position) == lineNumber;
        }

        private SourceLocation GetDeclarationLocationOfEnclosingFunction(int stepIndex)
        {
            var functionName = GetFunctionNameForStep(stepIndex);
            if (string.IsNullOrEmpty(functionName))
                return null;

            return GetFunctionDeclarationLocation(functionName, Trace[stepIndex].CodeAddress);
        }

        private SourceLocation FindSourceLocationInFollowingSteps(int stepIndex, int maxLookahead)
        {
            var trace = Trace[stepIndex];
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

        private SourceLocation FindDeclarationInSourcesOfMatchingContracts(string functionName, string contractName)
        {
            foreach (var kvp in _loadedContracts)
            {
                var abiInfo = kvp.Value;
                if (!HasIndexedSources(abiInfo))
                    continue;

                if (IsDifferentlyNamedContract(abiInfo, contractName))
                    continue;

                var referencedIndices = GetReferencedSourceFileIndices(kvp.Key);
                foreach (var fileEntry in abiInfo.SourceFileIndex)
                {
                    if (referencedIndices.Count > 0 && !referencedIndices.Contains(fileEntry.Key))
                        continue;

                    var loc = FindDeclarationInSourceFile(abiInfo, fileEntry, functionName);
                    if (loc != null) return loc;
                }
            }

            return null;
        }

        private SourceLocation FindDeclarationInSourcesOfOtherContracts(string functionName, string contractName)
        {
            foreach (var kvp in _loadedContracts)
            {
                var abiInfo = kvp.Value;
                if (!HasIndexedSources(abiInfo))
                    continue;
                if (string.Equals(abiInfo.ContractName, contractName, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var fileEntry in abiInfo.SourceFileIndex)
                {
                    var loc = FindDeclarationInSourceFile(abiInfo, fileEntry, functionName);
                    if (loc != null) return loc;
                }
            }

            return null;
        }

        private static bool IsDifferentlyNamedContract(ABIInfo abiInfo, string contractName)
        {
            if (string.IsNullOrEmpty(contractName) || string.IsNullOrEmpty(abiInfo.ContractName))
                return false;

            return !string.Equals(abiInfo.ContractName, contractName, StringComparison.OrdinalIgnoreCase);
        }

        private static SourceLocation FindDeclarationInSourceFile(
            ABIInfo abiInfo, KeyValuePair<int, string> fileEntry, string functionName)
        {
            var fileContent = abiInfo.GetSourceContent(fileEntry.Key);
            if (string.IsNullOrEmpty(fileContent))
                return null;

            return BuildFunctionLocation(fileEntry.Value, fileContent, fileEntry.Key, functionName);
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
    }
}
