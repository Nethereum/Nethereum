using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.CompilationMetadata;
using Nethereum.ABI.Model;
using Newtonsoft.Json.Linq;

namespace Nethereum.ABI.ABIRepository
{
    public class FileSystemABIInfoStorage : IABIInfoStorage
    {
        private readonly ABIInfoInMemoryStorage _inner = new ABIInfoInMemoryStorage();
        private readonly Dictionary<string, ABIInfo> _contractsByName = new Dictionary<string, ABIInfo>(StringComparer.OrdinalIgnoreCase);
        private bool _loaded;
        private readonly string _path;
        private readonly string _sourceBasePath;

        public FileSystemABIInfoStorage(string path) : this(path, null)
        {
        }

        public FileSystemABIInfoStorage(string path, string sourceBasePath)
        {
            _path = path;
            _sourceBasePath = sourceBasePath;
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            if (string.IsNullOrEmpty(_path) || !Directory.Exists(_path))
                return;

            var buildInfoMaps = LoadBuildInfoMaps();

            foreach (var file in Directory.EnumerateFiles(_path, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var normalizedFile = file.Replace('\\', '/');
                    if (normalizedFile.Contains("/build-info/"))
                        continue;

                    var json = File.ReadAllText(file);
                    var contractName = Path.GetFileNameWithoutExtension(file);
                    var results = ParseArtifact(json, contractName, buildInfoMaps);

                    foreach (var abiInfo in results)
                    {
                        _inner.AddABIInfo(abiInfo);
                        var name = abiInfo.ContractName ?? contractName;
                        if (!_contractsByName.ContainsKey(name))
                            _contractsByName[name] = abiInfo;
                    }
                }
                catch
                {
                }
            }
        }

        private List<Dictionary<int, string>> LoadBuildInfoMaps()
        {
            var result = new List<Dictionary<int, string>>();

            var buildInfoDir = Path.Combine(_path, "build-info");
            if (!Directory.Exists(buildInfoDir))
                return result;

            foreach (var file in Directory.EnumerateFiles(buildInfoDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var obj = JObject.Parse(json);

                    var sourceIdToPath = obj["source_id_to_path"] as JObject;
                    if (sourceIdToPath != null)
                    {
                        var map = new Dictionary<int, string>();
                        foreach (var kvp in sourceIdToPath)
                        {
                            if (int.TryParse(kvp.Key, out var id))
                                map[id] = kvp.Value.ToString();
                        }
                        if (map.Count > 0)
                            result.Add(map);
                        continue;
                    }

                    var hardhatResults = TryParseHardhatBuildInfo(obj);
                    foreach (var abiInfo in hardhatResults)
                    {
                        _inner.AddABIInfo(abiInfo);
                        if (!string.IsNullOrEmpty(abiInfo.ContractName) && !_contractsByName.ContainsKey(abiInfo.ContractName))
                            _contractsByName[abiInfo.ContractName] = abiInfo;
                    }
                }
                catch
                {
                }
            }

            return result;
        }

        private List<ABIInfo> ParseArtifact(string json, string contractName, List<Dictionary<int, string>> buildInfoMaps)
        {
            var results = new List<ABIInfo>();
            json = json.TrimStart();

            if (json.StartsWith("["))
            {
                try
                {
                    var abiInfo = ABIInfo.FromABI(json);
                    abiInfo.ContractName = contractName;
                    results.Add(abiInfo);
                }
                catch { }
                return results;
            }

            if (!json.StartsWith("{"))
                return results;

            try
            {
                var obj = JObject.Parse(json);

                if (IsFoundryArtifact(obj))
                {
                    var abiInfo = ParseFoundryArtifact(obj, contractName, buildInfoMaps);
                    if (abiInfo != null)
                        results.Add(abiInfo);
                    return results;
                }

                if (IsSolcStandardOutput(obj))
                {
                    results.AddRange(ParseSolcStandardOutput(obj));
                    return results;
                }

                var abiToken = obj["abi"];
                if (abiToken is JArray)
                {
                    var abiInfo = ABIInfo.FromABI(abiToken.ToString(Newtonsoft.Json.Formatting.None));
                    abiInfo.ContractName = contractName;

                    TryExtractSolcEvm(obj, abiInfo);

                    results.Add(abiInfo);
                }
            }
            catch { }

            return results;
        }

        private static bool IsFoundryArtifact(JObject obj)
        {
            return obj["abi"] is JArray
                && obj["deployedBytecode"] is JObject db
                && db["sourceMap"] != null;
        }

        private static bool IsSolcStandardOutput(JObject obj)
        {
            return obj["contracts"] is JObject
                && obj["sources"] is JObject
                && obj["abi"] == null;
        }

        private ABIInfo ParseFoundryArtifact(JObject obj, string contractName, List<Dictionary<int, string>> buildInfoMaps)
        {
            var abiToken = obj["abi"] as JArray;
            if (abiToken == null)
                return null;

            var abiString = abiToken.ToString(Newtonsoft.Json.Formatting.None);
            var abiInfo = ABIInfo.FromABI(abiString);
            abiInfo.ContractName = contractName;

            var deployedBytecode = obj["deployedBytecode"] as JObject;
            if (deployedBytecode != null)
            {
                var sourceMap = deployedBytecode["sourceMap"]?.ToString();
                if (!string.IsNullOrEmpty(sourceMap))
                    abiInfo.RuntimeSourceMap = sourceMap;

                var bytecodeObj = deployedBytecode["object"]?.ToString();
                if (!string.IsNullOrEmpty(bytecodeObj))
                    abiInfo.RuntimeBytecode = bytecodeObj;
            }

            var sourceFileId = obj["id"] != null ? obj["id"].Value<int>() : -1;

            var rawMetadata = obj["rawMetadata"]?.ToString();
            if (!string.IsNullOrEmpty(rawMetadata))
            {
                try
                {
                    var metadata = CompilationMetadataDeserialiser.DeserialiseCompilationMetadata(rawMetadata);
                    if (metadata != null)
                        abiInfo.Metadata = metadata;
                }
                catch { }
            }

            var expectedSourcePath = FindSourcePathFromMetadata(obj, abiInfo.Metadata);
            var sourceFileIndex = FindBuildInfoMap(buildInfoMaps, sourceFileId, expectedSourcePath);
            if (sourceFileIndex != null)
            {
                abiInfo.SourceFileIndex = sourceFileIndex;
                EnsureMetadataWithSources(abiInfo, sourceFileIndex);
            }
            else if (sourceFileId >= 0)
            {
                abiInfo.SourceFileIndex = new Dictionary<int, string>();
                if (!string.IsNullOrEmpty(expectedSourcePath))
                    abiInfo.SourceFileIndex[sourceFileId] = expectedSourcePath;
            }

            PopulateSourceContent(abiInfo.Metadata);

            return abiInfo;
        }

        private static Dictionary<int, string> FindBuildInfoMap(List<Dictionary<int, string>> buildInfoMaps, int sourceFileId, string expectedSourcePath = null)
        {
            if (sourceFileId < 0 || buildInfoMaps == null)
                return null;

            if (!string.IsNullOrEmpty(expectedSourcePath))
            {
                foreach (var map in buildInfoMaps)
                {
                    if (map.TryGetValue(sourceFileId, out var path) &&
                        string.Equals(path, expectedSourcePath, StringComparison.OrdinalIgnoreCase))
                        return new Dictionary<int, string>(map);
                }
            }

            foreach (var map in buildInfoMaps)
            {
                if (map.ContainsKey(sourceFileId))
                    return new Dictionary<int, string>(map);
            }

            return null;
        }

        private static string FindSourcePathFromMetadata(JObject obj, CompilationMetadata.CompilationMetadata metadata)
        {
            if (metadata?.Sources != null && metadata.Sources.Count > 0)
                return metadata.Sources.Keys.First();

            var metadataSection = obj["metadata"] as JObject;
            if (metadataSection != null)
            {
                var sources = metadataSection["sources"] as JObject;
                if (sources != null)
                {
                    foreach (var kvp in sources)
                        return kvp.Key;
                }
            }

            return null;
        }

        private void EnsureMetadataWithSources(ABIInfo abiInfo, Dictionary<int, string> sourceFileIndex)
        {
            if (abiInfo.Metadata == null)
                abiInfo.Metadata = new CompilationMetadata.CompilationMetadata();

            if (abiInfo.Metadata.Sources == null)
                abiInfo.Metadata.Sources = new Dictionary<string, SourceCode>();

            foreach (var kvp in sourceFileIndex)
            {
                if (!abiInfo.Metadata.Sources.ContainsKey(kvp.Value))
                {
                    abiInfo.Metadata.Sources[kvp.Value] = new SourceCode();
                }
            }
        }

        private List<ABIInfo> TryParseHardhatBuildInfo(JObject obj)
        {
            var results = new List<ABIInfo>();

            var input = obj["input"] as JObject;
            var output = obj["output"] as JObject;
            if (input == null || output == null)
                return results;

            var inputSources = input["sources"] as JObject;
            var outputSources = output["sources"] as JObject;
            var outputContracts = output["contracts"] as JObject;
            if (outputContracts == null)
                return results;

            var sourceIdToPath = new Dictionary<int, string>();
            if (outputSources != null)
            {
                foreach (var kvp in outputSources)
                {
                    var sourceObj = kvp.Value as JObject;
                    if (sourceObj != null && sourceObj["id"] != null)
                    {
                        sourceIdToPath[sourceObj["id"].Value<int>()] = kvp.Key;
                    }
                }
            }

            var sourceContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (inputSources != null)
            {
                foreach (var kvp in inputSources)
                {
                    var content = (kvp.Value as JObject)?["content"]?.ToString();
                    if (!string.IsNullOrEmpty(content))
                        sourceContents[kvp.Key] = content;
                }
            }

            foreach (var pathKvp in outputContracts)
            {
                var contractsAtPath = pathKvp.Value as JObject;
                if (contractsAtPath == null) continue;

                foreach (var contractKvp in contractsAtPath)
                {
                    try
                    {
                        var contractObj = contractKvp.Value as JObject;
                        if (contractObj == null) continue;

                        var abiInfo = ParseHardhatContract(contractKvp.Key, contractObj, sourceIdToPath, sourceContents);
                        if (abiInfo != null)
                            results.Add(abiInfo);
                    }
                    catch { }
                }
            }

            return results;
        }

        private ABIInfo ParseHardhatContract(string contractName, JObject contractObj,
            Dictionary<int, string> sourceIdToPath, Dictionary<string, string> sourceContents)
        {
            var abiToken = contractObj["abi"] as JArray;
            if (abiToken == null || abiToken.Count == 0)
                return null;

            var abiString = abiToken.ToString(Newtonsoft.Json.Formatting.None);
            var abiInfo = ABIInfo.FromABI(abiString);
            abiInfo.ContractName = contractName;

            var evm = contractObj["evm"] as JObject;
            if (evm != null)
            {
                var deployedBytecode = evm["deployedBytecode"] as JObject;
                if (deployedBytecode != null)
                {
                    var sourceMap = deployedBytecode["sourceMap"]?.ToString();
                    if (!string.IsNullOrEmpty(sourceMap))
                        abiInfo.RuntimeSourceMap = sourceMap;

                    var bytecodeObj = deployedBytecode["object"]?.ToString();
                    if (!string.IsNullOrEmpty(bytecodeObj))
                        abiInfo.RuntimeBytecode = bytecodeObj;
                }
            }

            var metadataStr = contractObj["metadata"]?.ToString();
            if (!string.IsNullOrEmpty(metadataStr))
            {
                try
                {
                    var metadata = CompilationMetadataDeserialiser.DeserialiseCompilationMetadata(metadataStr);
                    if (metadata != null)
                        abiInfo.Metadata = metadata;
                }
                catch { }
            }

            if (sourceIdToPath.Count > 0)
            {
                abiInfo.SourceFileIndex = new Dictionary<int, string>(sourceIdToPath);
                EnsureMetadataWithSources(abiInfo, sourceIdToPath);
            }

            if (abiInfo.Metadata?.Sources != null)
            {
                foreach (var kvp in abiInfo.Metadata.Sources.ToList())
                {
                    if (string.IsNullOrEmpty(kvp.Value.Content) && sourceContents.TryGetValue(kvp.Key, out var content))
                    {
                        kvp.Value.Content = content;
                    }
                }
            }

            PopulateSourceContent(abiInfo.Metadata);

            return abiInfo;
        }

        private List<ABIInfo> ParseSolcStandardOutput(JObject obj)
        {
            var results = new List<ABIInfo>();

            var contracts = obj["contracts"] as JObject;
            var sources = obj["sources"] as JObject;
            if (contracts == null) return results;

            var sourceIdToPath = new Dictionary<int, string>();
            if (sources != null)
            {
                foreach (var kvp in sources)
                {
                    var sourceObj = kvp.Value as JObject;
                    if (sourceObj != null && sourceObj["id"] != null)
                    {
                        sourceIdToPath[sourceObj["id"].Value<int>()] = kvp.Key;
                    }
                }
            }

            foreach (var pathKvp in contracts)
            {
                var contractsAtPath = pathKvp.Value as JObject;
                if (contractsAtPath == null) continue;

                foreach (var contractKvp in contractsAtPath)
                {
                    try
                    {
                        var contractObj = contractKvp.Value as JObject;
                        if (contractObj == null) continue;

                        var abiInfo = ParseHardhatContract(contractKvp.Key, contractObj, sourceIdToPath, new Dictionary<string, string>());
                        if (abiInfo != null)
                        {
                            PopulateSourceContent(abiInfo.Metadata);
                            results.Add(abiInfo);
                        }
                    }
                    catch { }
                }
            }

            return results;
        }

        private static void TryExtractSolcEvm(JObject obj, ABIInfo abiInfo)
        {
            var evm = obj["evm"] as JObject;
            if (evm == null) return;

            var deployedBytecode = evm["deployedBytecode"] as JObject;
            if (deployedBytecode != null)
            {
                var sourceMap = deployedBytecode["sourceMap"]?.ToString();
                if (!string.IsNullOrEmpty(sourceMap))
                    abiInfo.RuntimeSourceMap = sourceMap;

                var bytecodeObj = deployedBytecode["object"]?.ToString();
                if (!string.IsNullOrEmpty(bytecodeObj))
                    abiInfo.RuntimeBytecode = bytecodeObj;
            }
        }

        private void PopulateSourceContent(CompilationMetadata.CompilationMetadata metadata)
        {
            if (metadata?.Sources == null)
                return;

            var basePath = !string.IsNullOrEmpty(_sourceBasePath) ? _sourceBasePath : _path;

            foreach (var kvp in metadata.Sources.ToList())
            {
                if (string.IsNullOrEmpty(kvp.Value.Content))
                {
                    var content = TryReadSourceFile(basePath, kvp.Key);
                    if (content != null)
                    {
                        kvp.Value.Content = content;
                    }
                }
            }
        }

        private static string TryReadSourceFile(string basePath, string relativePath)
        {
            try
            {
                var fullPath = Path.Combine(basePath, relativePath);
                if (File.Exists(fullPath))
                    return NormalizeLineEndings(File.ReadAllText(fullPath));

                var parentDir = Directory.GetParent(basePath)?.FullName;
                if (parentDir != null)
                {
                    fullPath = Path.Combine(parentDir, relativePath);
                    if (File.Exists(fullPath))
                        return NormalizeLineEndings(File.ReadAllText(fullPath));
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeLineEndings(string content)
        {
            if (content == null) return null;
            return content.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        public ABIInfo GetABIInfoByContractName(string contractName)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(contractName))
                return null;
            _contractsByName.TryGetValue(contractName, out var abiInfo);
            return abiInfo;
        }

        public ABIInfo FindABIInfoByRuntimeBytecode(string runtimeBytecodeHex)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(runtimeBytecodeHex))
                return null;

            var normalized = NormalizeBytecodeForComparison(runtimeBytecodeHex);
            if (string.IsNullOrEmpty(normalized))
                return null;

            foreach (var kvp in _contractsByName)
            {
                var candidate = kvp.Value;
                if (string.IsNullOrEmpty(candidate.RuntimeBytecode))
                    continue;

                var candidateNormalized = NormalizeBytecodeForComparison(candidate.RuntimeBytecode);
                if (string.IsNullOrEmpty(candidateNormalized))
                    continue;

                if (normalized.StartsWith(candidateNormalized) || candidateNormalized.StartsWith(normalized))
                    return candidate;
            }

            return null;
        }

        private static string NormalizeBytecodeForComparison(string bytecode)
        {
            if (string.IsNullOrEmpty(bytecode))
                return null;

            bytecode = bytecode.Trim();
            if (bytecode.StartsWith("0x") || bytecode.StartsWith("0X"))
                bytecode = bytecode.Substring(2);

            if (bytecode.Length < 10)
                return null;

            var metadataMarkerIndex = bytecode.LastIndexOf("a264");
            if (metadataMarkerIndex > 0)
                bytecode = bytecode.Substring(0, metadataMarkerIndex);

            return bytecode.ToLowerInvariant();
        }

        public void RegisterContractAddress(string contractName, string address, long chainId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(contractName) || string.IsNullOrEmpty(address))
                return;

            if (!_contractsByName.TryGetValue(contractName, out var source))
                return;

            var addressBound = new ABIInfo
            {
                ABI = source.ABI,
                ContractName = source.ContractName,
                Address = address.ToLowerInvariant(),
                ChainId = chainId,
                RuntimeSourceMap = source.RuntimeSourceMap,
                RuntimeBytecode = source.RuntimeBytecode,
                Metadata = source.Metadata,
                SourceFileIndex = source.SourceFileIndex != null
                    ? new Dictionary<int, string>(source.SourceFileIndex)
                    : null
            };

            _inner.AddABIInfo(addressBound);
        }

        public void AddABIInfo(ABIInfo abiInfo) { EnsureLoaded(); _inner.AddABIInfo(abiInfo); }
        public ABIInfo GetABIInfo(BigInteger chainId, string contractAddress) { EnsureLoaded(); return _inner.GetABIInfo(chainId, contractAddress); }
        public FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature) { EnsureLoaded(); return _inner.FindFunctionABI(chainId, contractAddress, signature); }
        public List<FunctionABI> FindFunctionABI(string signature) { EnsureLoaded(); return _inner.FindFunctionABI(signature); }
        public FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData) { EnsureLoaded(); return _inner.FindFunctionABIFromInputData(chainId, contractAddress, inputData); }
        public List<FunctionABI> FindFunctionABIFromInputData(string inputData) { EnsureLoaded(); return _inner.FindFunctionABIFromInputData(inputData); }
        public EventABI FindEventABI(BigInteger chainId, string contractAddress, string signature) { EnsureLoaded(); return _inner.FindEventABI(chainId, contractAddress, signature); }
        public List<EventABI> FindEventABI(string signature) { EnsureLoaded(); return _inner.FindEventABI(signature); }
        public ErrorABI FindErrorABI(BigInteger chainId, string contractAddress, string signature) { EnsureLoaded(); return _inner.FindErrorABI(chainId, contractAddress, signature); }
        public List<ErrorABI> FindErrorABI(string signature) { EnsureLoaded(); return _inner.FindErrorABI(signature); }

        public Task<ABIInfo> GetABIInfoAsync(long chainId, string contractAddress) { EnsureLoaded(); return _inner.GetABIInfoAsync(chainId, contractAddress); }
        public Task<FunctionABI> FindFunctionABIAsync(BigInteger chainId, string contractAddress, string signature) { EnsureLoaded(); return _inner.FindFunctionABIAsync(chainId, contractAddress, signature); }
        public Task<FunctionABI> FindFunctionABIFromInputDataAsync(BigInteger chainId, string contractAddress, string inputData) { EnsureLoaded(); return _inner.FindFunctionABIFromInputDataAsync(chainId, contractAddress, inputData); }
        public Task<EventABI> FindEventABIAsync(BigInteger chainId, string contractAddress, string signature) { EnsureLoaded(); return _inner.FindEventABIAsync(chainId, contractAddress, signature); }
        public Task<ErrorABI> FindErrorABIAsync(BigInteger chainId, string contractAddress, string signature) { EnsureLoaded(); return _inner.FindErrorABIAsync(chainId, contractAddress, signature); }
        public Task<IDictionary<string, FunctionABI>> FindFunctionABIsBatchAsync(IEnumerable<string> signatures) { EnsureLoaded(); return _inner.FindFunctionABIsBatchAsync(signatures); }
        public Task<IDictionary<string, EventABI>> FindEventABIsBatchAsync(IEnumerable<string> signatures) { EnsureLoaded(); return _inner.FindEventABIsBatchAsync(signatures); }
        public Task<ABIBatchResult> FindABIsBatchAsync(IEnumerable<string> functionSignatures, IEnumerable<string> eventSignatures) { EnsureLoaded(); return _inner.FindABIsBatchAsync(functionSignatures, eventSignatures); }
    }
}
