using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.CompilationMetadata;
using Nethereum.ABI.Model;
using Nethereum.DataServices.Sourcify;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

#if NET8_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif

namespace Nethereum.DataServices.ABIInfoStorage
{
    public class SourcifyABIInfoStorage : IABIInfoStorage
    {
        private readonly SourcifyApiServiceV2 _sourcify;
        private readonly bool _resolveProxies;

        public SourcifyABIInfoStorage() : this(new SourcifyApiServiceV2(), resolveProxies: true)
        {
        }

        public SourcifyABIInfoStorage(SourcifyApiServiceV2 sourcify, bool resolveProxies = true)
        {
            _sourcify = sourcify;
            _resolveProxies = resolveProxies;
        }

        public void AddABIInfo(ABIInfo abiInfo)
        {
        }

        public ABIInfo GetABIInfo(BigInteger chainId, string contractAddress)
        {
            return GetABIInfoAsync((long)chainId, contractAddress).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<ABIInfo> GetABIInfoAsync(long chainId, string contractAddress)
        {
            if (string.IsNullOrEmpty(contractAddress))
                return null;

            return await FetchFromSourcifyAsync(chainId, contractAddress).ConfigureAwait(false);
        }

        public FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            return abiInfo?.ContractABI?.FindFunctionABI(signature);
        }

        public FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            return abiInfo?.ContractABI?.FindFunctionABIFromInputData(inputData);
        }

        public EventABI FindEventABI(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            return abiInfo?.ContractABI?.FindEventABI(signature);
        }

        public ErrorABI FindErrorABI(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            return abiInfo?.ContractABI?.FindErrorABI(signature);
        }

        public List<FunctionABI> FindFunctionABI(string signature)
        {
            return new List<FunctionABI>();
        }

        public List<FunctionABI> FindFunctionABIFromInputData(string inputData)
        {
            return new List<FunctionABI>();
        }

        public List<EventABI> FindEventABI(string signature)
        {
            return new List<EventABI>();
        }

        public List<ErrorABI> FindErrorABI(string signature)
        {
            return new List<ErrorABI>();
        }

        public async Task<FunctionABI> FindFunctionABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = await GetABIInfoAsync((long)chainId, contractAddress).ConfigureAwait(false);
            return abiInfo?.ContractABI?.FindFunctionABI(signature);
        }

        public async Task<FunctionABI> FindFunctionABIFromInputDataAsync(BigInteger chainId, string contractAddress, string inputData)
        {
            var abiInfo = await GetABIInfoAsync((long)chainId, contractAddress).ConfigureAwait(false);
            return abiInfo?.ContractABI?.FindFunctionABIFromInputData(inputData);
        }

        public async Task<EventABI> FindEventABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = await GetABIInfoAsync((long)chainId, contractAddress).ConfigureAwait(false);
            return abiInfo?.ContractABI?.FindEventABI(signature);
        }

        public async Task<ErrorABI> FindErrorABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = await GetABIInfoAsync((long)chainId, contractAddress).ConfigureAwait(false);
            return abiInfo?.ContractABI?.FindErrorABI(signature);
        }

        public Task<IDictionary<string, FunctionABI>> FindFunctionABIsBatchAsync(IEnumerable<string> signatures)
        {
            return Task.FromResult<IDictionary<string, FunctionABI>>(new Dictionary<string, FunctionABI>());
        }

        public Task<IDictionary<string, EventABI>> FindEventABIsBatchAsync(IEnumerable<string> signatures)
        {
            return Task.FromResult<IDictionary<string, EventABI>>(new Dictionary<string, EventABI>());
        }

        public Task<ABIBatchResult> FindABIsBatchAsync(IEnumerable<string> functionSignatures, IEnumerable<string> eventSignatures)
        {
            return Task.FromResult(new ABIBatchResult());
        }

        private async Task<ABIInfo> FetchFromSourcifyAsync(long chainId, string address)
        {
            try
            {
                var fields = _resolveProxies
                    ? "abi,compilation,proxyResolution,sources,runtimeBytecode,sourceIds"
                    : "abi,compilation,sources,runtimeBytecode,sourceIds";

                var response = await _sourcify.GetContractAsync(chainId, address, fields: fields).ConfigureAwait(false);
                if (response?.Abi == null) return null;

                var abiString = response.GetAbiString();
                if (string.IsNullOrEmpty(abiString)) return null;

                var abiInfo = ABIInfo.FromABI(
                    abiString,
                    address?.ToLowerInvariant(),
                    response.Compilation?.ContractName,
                    null,
                    chainId);

                if (response.RuntimeBytecode != null)
                {
                    abiInfo.RuntimeBytecode = response.RuntimeBytecode.OnchainBytecode;
                    abiInfo.RuntimeSourceMap = response.RuntimeBytecode.SourceMap;
                }

                if (response.Sources != null && response.Sources.Count > 0)
                {
                    abiInfo.Metadata = new Nethereum.ABI.CompilationMetadata.CompilationMetadata
                    {
                        Sources = new Dictionary<string, SourceCode>()
                    };
                    foreach (var source in response.Sources)
                    {
                        abiInfo.Metadata.Sources[source.Key] = new SourceCode
                        {
                            Content = source.Value.Content
                        };
                    }
                }

                try
                {
                    abiInfo.SourceFileIndex = BuildSourceFileIndex(response.SourceIds);
                }
                catch
                {
                }

                if (_resolveProxies && response.ProxyResolution?.IsProxy == true && response.ProxyResolution.Implementations != null)
                {
                    abiInfo.ProxyImplementationAddresses = response.ProxyResolution.Implementations
                        .Where(i => !string.IsNullOrEmpty(i.Address))
                        .Select(i => i.Address.ToLowerInvariant())
                        .ToList();
                }

                return abiInfo;
            }
            catch
            {
                return null;
            }
        }

#if NET8_0_OR_GREATER
        private Dictionary<int, string> BuildSourceFileIndex(JsonElement? sourceIds)
        {
            if (sourceIds == null || sourceIds.Value.ValueKind == JsonValueKind.Null)
                return null;

            var result = new Dictionary<int, string>();
            foreach (var prop in sourceIds.Value.EnumerateObject())
            {
                if (prop.Value.TryGetInt32(out int index))
                {
                    result[index] = prop.Name;
                }
                else if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    if (prop.Value.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out int objIndex))
                    {
                        result[objIndex] = prop.Name;
                    }
                }
                else if (prop.Value.ValueKind == JsonValueKind.String && int.TryParse(prop.Name, out int keyIndex))
                {
                    result[keyIndex] = prop.Value.GetString();
                }
            }
            return result.Count > 0 ? result : null;
        }
#else
        private Dictionary<int, string> BuildSourceFileIndex(object sourceIds)
        {
            if (sourceIds == null) return null;

            var result = new Dictionary<int, string>();

            if (sourceIds is JObject jobj)
            {
                foreach (var prop in jobj.Properties())
                {
                    if (prop.Value.Type == JTokenType.Integer)
                    {
                        result[(int)prop.Value] = prop.Name;
                    }
                    else if (prop.Value.Type == JTokenType.Object)
                    {
                        var idToken = prop.Value["id"];
                        if (idToken != null && idToken.Type == JTokenType.Integer)
                        {
                            result[(int)idToken] = prop.Name;
                        }
                    }
                    else if (prop.Value.Type == JTokenType.String && int.TryParse(prop.Name, out int keyIndex))
                    {
                        result[keyIndex] = prop.Value.ToString();
                    }
                }
            }
            else if (sourceIds is IDictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    if (kvp.Value is int index)
                    {
                        result[index] = kvp.Key;
                    }
                    else if (kvp.Value is long lindex)
                    {
                        result[(int)lindex] = kvp.Key;
                    }
                    else if (kvp.Value is IDictionary<string, object> objDict && objDict.TryGetValue("id", out var idVal))
                    {
                        if (idVal is int objIndex)
                        {
                            result[objIndex] = kvp.Key;
                        }
                        else if (idVal is long objLindex)
                        {
                            result[(int)objLindex] = kvp.Key;
                        }
                    }
                }
            }

            return result.Count > 0 ? result : null;
        }
#endif
    }
}
