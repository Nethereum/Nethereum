using Nethereum.ABI.ABIRepository;
using Nethereum.EVM.SourceInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.EVM.Debugging
{
    public partial class EVMDebuggerSession
    {
        private readonly IABIInfoStorage _abiStorage;

        public void SetContractDebugInfo(string address, ABIInfo abiInfo)
        {
            if (abiInfo == null) return;
            var normalizedAddress = address?.ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedAddress)) return;

            _loadedContracts[normalizedAddress] = abiInfo;
            _checkedAddresses.Add(normalizedAddress);
            CacheSourceMaps(normalizedAddress, abiInfo);
            BuildContractDebugIndexes(normalizedAddress, abiInfo);
        }

        private Dictionary<string, ABIInfo> _loadedContracts = new Dictionary<string, ABIInfo>();
        private Dictionary<string, List<SourceMap>> _contractSourceMaps = new Dictionary<string, List<SourceMap>>();
        private Dictionary<string, Dictionary<int, int>> _pcToInstructionIndex = new Dictionary<string, Dictionary<int, int>>();
        private Dictionary<string, Dictionary<int, string>> _functionMaps = new Dictionary<string, Dictionary<int, string>>();
        private HashSet<string> _checkedAddresses = new HashSet<string>();
        private BigInteger _chainId;

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

        public string GetContractNameForAddress(string address)
        {
            var abiInfo = GetABIInfoForAddress(address);
            return abiInfo?.ContractName;
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
                if (!HasIndexedSources(abiInfo))
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

        private static bool HasIndexedSources(ABIInfo abiInfo) =>
            abiInfo?.SourceFileIndex != null && abiInfo.Metadata?.Sources != null;

        private void ResetDebugInfoCaches()
        {
            _loadedContracts.Clear();
            _contractSourceMaps.Clear();
            _pcToInstructionIndex.Clear();
            _functionMaps.Clear();
            _functionNameCache.Clear();
            _checkedAddresses.Clear();
        }

        private void ResetDebugInfoCachesExceptFunctionMaps()
        {
            _loadedContracts.Clear();
            _contractSourceMaps.Clear();
            _pcToInstructionIndex.Clear();
            _functionNameCache.Clear();
            _checkedAddresses.Clear();
        }

        private IEnumerable<string> DistinctTracedCodeAddresses() =>
            Trace.Select(t => t.CodeAddress).Where(a => !string.IsNullOrEmpty(a)).Distinct();

        private void LoadDebugInfoForTracedContracts()
        {
            foreach (var address in DistinctTracedCodeAddresses())
            {
                LoadContractDebugInfo(address);
            }
        }

        private async Task LoadDebugInfoForTracedContractsAsync()
        {
            foreach (var address in DistinctTracedCodeAddresses())
            {
                await LoadContractDebugInfoAsync(address);
            }
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
                RegisterContractDebugInfo(normalizedAddress, abiInfo);
            }
        }

        private async Task LoadContractDebugInfoAsync(string address)
        {
            var normalizedAddress = address?.ToLowerInvariant();
            if (string.IsNullOrEmpty(normalizedAddress) || _checkedAddresses.Contains(normalizedAddress))
                return;

            _checkedAddresses.Add(normalizedAddress);
            var abiInfo = await _abiStorage.GetABIInfoAsync(ChainIdForAsyncLookup(), normalizedAddress);
            if (abiInfo != null)
            {
                RegisterContractDebugInfo(normalizedAddress, abiInfo);
            }
        }

        private long ChainIdForAsyncLookup() => _chainId > long.MaxValue ? 0L : (long)_chainId;

        private void RegisterContractDebugInfo(string normalizedAddress, ABIInfo abiInfo)
        {
            _loadedContracts[normalizedAddress] = abiInfo;
            CacheSourceMaps(normalizedAddress, abiInfo);
            BuildContractDebugIndexes(normalizedAddress, abiInfo);
        }

        private void CacheSourceMaps(string normalizedAddress, ABIInfo abiInfo)
        {
            if (string.IsNullOrEmpty(abiInfo.RuntimeSourceMap))
                return;

            _contractSourceMaps[normalizedAddress] = new SourceMapUtil().UnCompressSourceMap(abiInfo.RuntimeSourceMap);
        }

        private void BuildContractDebugIndexes(string normalizedAddress, ABIInfo abiInfo)
        {
            var instructions = DisassembleRuntimeBytecode(abiInfo);
            if (instructions == null)
                return;

            _pcToInstructionIndex[normalizedAddress] = MapProgramCountersToInstructionIndices(instructions);

            abiInfo.InitialiseContractABI();
            var funcMap = ProgramInstructionsUtils.GetFunctionDispatcherMap(instructions, abiInfo.ContractABI);
            if (funcMap.Count > 0)
                _functionMaps[normalizedAddress] = funcMap;
        }

        private static List<ProgramInstruction> DisassembleRuntimeBytecode(ABIInfo abiInfo)
        {
            if (string.IsNullOrEmpty(abiInfo?.RuntimeBytecode))
                return null;

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
                return null;
            }

            return ProgramInstructionsUtils.GetProgramInstructions(bytecodeBytes);
        }

        private static Dictionary<int, int> MapProgramCountersToInstructionIndices(List<ProgramInstruction> instructions)
        {
            var pcMap = new Dictionary<int, int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                pcMap[instructions[i].Step] = i;
            }
            return pcMap;
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
    }
}
