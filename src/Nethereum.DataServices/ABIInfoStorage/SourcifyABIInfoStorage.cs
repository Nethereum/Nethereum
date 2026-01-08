using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.CompilationMetadata;
using Nethereum.ABI.Model;
using Nethereum.DataServices.Sourcify;
using Nethereum.DataServices.Sourcify.Responses;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#if NET8_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#endif

namespace Nethereum.DataServices.ABIInfoStorage
{
    public class SourcifyABIInfoStorage : IABIInfoStorage
    {
        private class FetchWithProxyResult
        {
            public ABIInfo AbiInfo { get; set; }
            public List<string> Implementations { get; set; }
        }

        private readonly SourcifyApiServiceV2 _sourcify;
        private readonly Sourcify4ByteSignatureService _signatureService;
        private readonly ABIStringSignatureDeserialiser _signatureDeserialiser = new ABIStringSignatureDeserialiser();
        private readonly ABIInfoInMemoryStorage _cache = new ABIInfoInMemoryStorage();
        private readonly Dictionary<string, List<string>> _proxyToImplementations = new Dictionary<string, List<string>>();
        private readonly HashSet<string> _loadedAddresses = new HashSet<string>();
        private readonly bool _resolveProxies;
        private readonly bool _use4ByteFallback;

        public SourcifyABIInfoStorage() : this(new SourcifyApiServiceV2(), new Sourcify4ByteSignatureService(), resolveProxies: true, use4ByteFallback: true)
        {
        }

        public SourcifyABIInfoStorage(SourcifyApiServiceV2 sourcify, bool resolveProxies = true)
            : this(sourcify, new Sourcify4ByteSignatureService(), resolveProxies, use4ByteFallback: true)
        {
        }

        public SourcifyABIInfoStorage(SourcifyApiServiceV2 sourcify, Sourcify4ByteSignatureService signatureService, bool resolveProxies = true, bool use4ByteFallback = true)
        {
            _sourcify = sourcify;
            _signatureService = signatureService;
            _resolveProxies = resolveProxies;
            _use4ByteFallback = use4ByteFallback;
        }

        public void AddABIInfo(ABIInfo abiInfo)
        {
            _cache.AddABIInfo(abiInfo);
        }

        public ABIInfo GetABIInfo(BigInteger chainId, string contractAddress)
        {
            var cached = _cache.GetABIInfo(chainId, contractAddress);
            if (cached != null) return cached;

            var abiInfo = FetchFromSourcifyAsync((long)chainId, contractAddress).ConfigureAwait(false).GetAwaiter().GetResult();
            if (abiInfo != null)
            {
                _cache.AddABIInfo(abiInfo);
            }
            return abiInfo;
        }

        public async Task<ABIInfo> GetABIInfoAsync(long chainId, string contractAddress)
        {
            var cached = _cache.GetABIInfo(chainId, contractAddress);
            if (cached != null) return cached;

            var abiInfo = await FetchFromSourcifyAsync(chainId, contractAddress).ConfigureAwait(false);
            if (abiInfo != null)
            {
                _cache.AddABIInfo(abiInfo);
            }
            return abiInfo;
        }

        public FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature)
        {
            EnsureLoaded(chainId, contractAddress);
            var result = _cache.FindFunctionABI(chainId, contractAddress, signature);
            if (result != null) return result;

            result = FindInImplementations(chainId, contractAddress,
                (cid, addr) => _cache.FindFunctionABI(cid, addr, signature));
            if (result != null) return result;

            if (_use4ByteFallback)
            {
                return LookupFunctionFrom4ByteService(signature);
            }
            return null;
        }

        public FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData)
        {
            EnsureLoaded(chainId, contractAddress);
            var result = _cache.FindFunctionABIFromInputData(chainId, contractAddress, inputData);
            if (result != null) return result;

            result = FindInImplementations(chainId, contractAddress,
                (cid, addr) => _cache.FindFunctionABIFromInputData(cid, addr, inputData));
            if (result != null) return result;

            if (_use4ByteFallback && !string.IsNullOrEmpty(inputData) && inputData.Length >= 10)
            {
                var selector = inputData.StartsWith("0x") ? inputData.Substring(0, 10) : "0x" + inputData.Substring(0, 8);
                return LookupFunctionFrom4ByteService(selector);
            }
            return null;
        }

        public EventABI FindEventABI(BigInteger chainId, string contractAddress, string signature)
        {
            EnsureLoaded(chainId, contractAddress);
            var result = _cache.FindEventABI(chainId, contractAddress, signature);
            if (result != null) return result;

            result = FindInImplementations(chainId, contractAddress,
                (cid, addr) => _cache.FindEventABI(cid, addr, signature));
            if (result != null) return result;

            if (_use4ByteFallback)
            {
                return LookupEventFrom4ByteService(signature);
            }
            return null;
        }

        public ErrorABI FindErrorABI(BigInteger chainId, string contractAddress, string signature)
        {
            EnsureLoaded(chainId, contractAddress);
            var result = _cache.FindErrorABI(chainId, contractAddress, signature);
            if (result != null) return result;

            return FindInImplementations(chainId, contractAddress,
                (cid, addr) => _cache.FindErrorABI(cid, addr, signature));
        }

        private T FindInImplementations<T>(BigInteger chainId, string contractAddress, System.Func<BigInteger, string, T> finder) where T : class
        {
            var normalizedAddress = contractAddress?.ToLowerInvariant();
            if (normalizedAddress != null && _proxyToImplementations.TryGetValue(normalizedAddress, out var implementations))
            {
                foreach (var implAddress in implementations)
                {
                    var result = finder(chainId, implAddress);
                    if (result != null) return result;
                }
            }
            return null;
        }

        public List<FunctionABI> FindFunctionABI(string signature)
        {
            var result = _cache.FindFunctionABI(signature);
            if (result != null && result.Count > 0) return result;

            if (_use4ByteFallback)
            {
                var fallback = LookupFunctionFrom4ByteService(signature);
                if (fallback != null) return new List<FunctionABI> { fallback };
            }
            return result ?? new List<FunctionABI>();
        }

        public List<FunctionABI> FindFunctionABIFromInputData(string inputData)
        {
            var result = _cache.FindFunctionABIFromInputData(inputData);
            if (result != null && result.Count > 0) return result;

            if (_use4ByteFallback && !string.IsNullOrEmpty(inputData) && inputData.Length >= 10)
            {
                var selector = inputData.StartsWith("0x") ? inputData.Substring(0, 10) : "0x" + inputData.Substring(0, 8);
                var fallback = LookupFunctionFrom4ByteService(selector);
                if (fallback != null) return new List<FunctionABI> { fallback };
            }
            return result ?? new List<FunctionABI>();
        }

        public List<EventABI> FindEventABI(string signature)
        {
            var result = _cache.FindEventABI(signature);
            if (result != null && result.Count > 0) return result;

            if (_use4ByteFallback)
            {
                var fallback = LookupEventFrom4ByteService(signature);
                if (fallback != null) return new List<EventABI> { fallback };
            }
            return result ?? new List<EventABI>();
        }

        public List<ErrorABI> FindErrorABI(string signature)
        {
            return _cache.FindErrorABI(signature);
        }

        private void EnsureLoaded(BigInteger chainId, string contractAddress)
        {
            var normalizedAddress = contractAddress?.ToLowerInvariant();
            if (normalizedAddress == null || _loadedAddresses.Contains(normalizedAddress))
                return;

            _loadedAddresses.Add(normalizedAddress);

            var result = FetchWithProxyResolutionAsync((long)chainId, contractAddress).ConfigureAwait(false).GetAwaiter().GetResult();
            if (result.AbiInfo != null)
            {
                _cache.AddABIInfo(result.AbiInfo);
            }

            if (result.Implementations != null && result.Implementations.Count > 0)
            {
                _proxyToImplementations[normalizedAddress] = result.Implementations;

                foreach (var implAddress in result.Implementations)
                {
                    EnsureLoaded(chainId, implAddress);
                }
            }
        }

        private async Task<FetchWithProxyResult> FetchWithProxyResolutionAsync(long chainId, string address)
        {
            try
            {
                var fields = _resolveProxies
                    ? "abi,compilation,proxyResolution,sources,runtimeBytecode,sourceIds"
                    : "abi,compilation,sources,runtimeBytecode,sourceIds";
                var response = await _sourcify.GetContractAsync(chainId, address, fields: fields).ConfigureAwait(false);
                if (response?.Abi == null) return new FetchWithProxyResult();

                var abiString = response.GetAbiString();
                if (string.IsNullOrEmpty(abiString)) return new FetchWithProxyResult();

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

                abiInfo.SourceFileIndex = BuildSourceFileIndex(response.SourceIds);

                List<string> implementations = null;
                if (_resolveProxies && response.ProxyResolution?.IsProxy == true && response.ProxyResolution.Implementations != null)
                {
                    implementations = response.ProxyResolution.Implementations
                        .Where(i => !string.IsNullOrEmpty(i.Address))
                        .Select(i => i.Address.ToLowerInvariant())
                        .ToList();
                }

                return new FetchWithProxyResult { AbiInfo = abiInfo, Implementations = implementations };
            }
            catch
            {
                return new FetchWithProxyResult();
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
                }
            }

            return result.Count > 0 ? result : null;
        }
#endif

        private async Task<ABIInfo> FetchFromSourcifyAsync(long chainId, string address)
        {
            var result = await FetchWithProxyResolutionAsync(chainId, address).ConfigureAwait(false);
            return result.AbiInfo;
        }

        private FunctionABI LookupFunctionFrom4ByteService(string selector)
        {
            if (_signatureService == null || string.IsNullOrEmpty(selector)) return null;
            try
            {
                var response = _signatureService.LookupFunctionAsync(selector)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                if (response?.Ok == true && response.Result?.Function != null)
                {
                    if (response.Result.Function.TryGetValue(selector, out var signatures) && signatures.Count > 0)
                    {
                        return ParseFunctionSignature(signatures[0].Name);
                    }
                }
            }
            catch { }
            return null;
        }

        private EventABI LookupEventFrom4ByteService(string signature)
        {
            if (_signatureService == null || string.IsNullOrEmpty(signature)) return null;
            try
            {
                var response = _signatureService.LookupEventAsync(signature)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                if (response?.Ok == true && response.Result?.Event != null)
                {
                    if (response.Result.Event.TryGetValue(signature, out var signatures) && signatures.Count > 0)
                    {
                        return ParseEventSignature(signatures[0].Name);
                    }
                }
            }
            catch { }
            return null;
        }

        private FunctionABI ParseFunctionSignature(string signature)
        {
            if (string.IsNullOrEmpty(signature)) return null;
            var match = Regex.Match(signature, @"^(\w+)\((.*)?\)$");
            if (!match.Success) return null;
            var name = match.Groups[1].Value;
            var parameters = match.Groups[2].Value;
            return _signatureDeserialiser.ExtractFunctionABI(signature, name, parameters);
        }

        private EventABI ParseEventSignature(string signature)
        {
            if (string.IsNullOrEmpty(signature)) return null;
            var match = Regex.Match(signature, @"^(\w+)\((.*)?\)$");
            if (!match.Success) return null;
            var name = match.Groups[1].Value;
            var parameters = match.Groups[2].Value;
            return _signatureDeserialiser.ExtractEventABI(signature, name, parameters);
        }
    }
}
