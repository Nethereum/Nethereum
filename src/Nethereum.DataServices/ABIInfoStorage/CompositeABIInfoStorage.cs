using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.DataServices.ABIInfoStorage
{
    public class CompositeABIInfoStorage : IABIInfoStorage
    {
        private readonly IABIInfoStorage _cache;
        private readonly List<IABIInfoStorage> _implementations;
        private readonly Dictionary<string, List<string>> _proxyToImplementations = new Dictionary<string, List<string>>();

        public CompositeABIInfoStorage(IABIInfoStorage cache, params IABIInfoStorage[] implementations)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _implementations = implementations?.ToList() ?? new List<IABIInfoStorage>();
        }

        public CompositeABIInfoStorage(IABIInfoStorage cache, IEnumerable<IABIInfoStorage> implementations)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _implementations = implementations?.ToList() ?? new List<IABIInfoStorage>();
        }

        public CompositeABIInfoStorage(params IABIInfoStorage[] storages)
        {
            if (storages == null || storages.Length == 0)
                throw new ArgumentException("At least one storage required", nameof(storages));
            _cache = storages[0];
            _implementations = storages.Skip(1).ToList();
        }

        public CompositeABIInfoStorage(IEnumerable<IABIInfoStorage> storages)
        {
            var list = storages?.ToList();
            if (list == null || list.Count == 0)
                throw new ArgumentException("At least one storage required", nameof(storages));
            _cache = list[0];
            _implementations = list.Skip(1).ToList();
        }

        public void AddABIInfo(ABIInfo abiInfo)
        {
            _cache.AddABIInfo(abiInfo);
        }

        public ABIInfo GetABIInfo(BigInteger chainId, string contractAddress)
        {
            return GetABIInfoAsync((long)chainId, contractAddress).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<ABIInfo> GetABIInfoAsync(long chainId, string contractAddress)
        {
            if (string.IsNullOrEmpty(contractAddress))
                return null;

            var cached = await _cache.GetABIInfoAsync(chainId, contractAddress).ConfigureAwait(false);
            if (cached != null) return cached;

            foreach (var impl in _implementations)
            {
                try
                {
                    var result = await impl.GetABIInfoAsync(chainId, contractAddress).ConfigureAwait(false);
                    if (result != null)
                    {
                        _cache.AddABIInfo(result);

                        if (result.ProxyImplementationAddresses?.Count > 0)
                        {
                            var key = GetCacheKey(chainId, contractAddress);
                            _proxyToImplementations[key] = result.ProxyImplementationAddresses;

                            foreach (var implAddress in result.ProxyImplementationAddresses)
                            {
                                await GetABIInfoAsync(chainId, implAddress).ConfigureAwait(false);
                            }
                        }

                        return result;
                    }
                }
                catch { }
            }
            return null;
        }

        public FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            var result = abiInfo?.ContractABI?.FindFunctionABI(signature);
            if (result != null) return result;

            return FindInImplementations(chainId, contractAddress,
                (cid, addr) => _cache.FindFunctionABI(cid, addr, signature));
        }

        public FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            var result = abiInfo?.ContractABI?.FindFunctionABIFromInputData(inputData);
            if (result != null) return result;

            return FindInImplementations(chainId, contractAddress,
                (cid, addr) => _cache.FindFunctionABIFromInputData(cid, addr, inputData));
        }

        public EventABI FindEventABI(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            var result = abiInfo?.ContractABI?.FindEventABI(signature);
            if (result != null) return result;

            return FindInImplementations(chainId, contractAddress,
                (cid, addr) => _cache.FindEventABI(cid, addr, signature));
        }

        public ErrorABI FindErrorABI(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            var result = abiInfo?.ContractABI?.FindErrorABI(signature);
            if (result != null) return result;

            return FindInImplementations(chainId, contractAddress,
                (cid, addr) => _cache.FindErrorABI(cid, addr, signature));
        }

        public List<FunctionABI> FindFunctionABI(string signature)
        {
            var results = new List<FunctionABI>();

            var cached = _cache.FindFunctionABI(signature);
            if (cached != null && cached.Count > 0)
                results.AddRange(cached);

            foreach (var impl in _implementations)
            {
                try
                {
                    var found = impl.FindFunctionABI(signature);
                    if (found != null && found.Count > 0)
                    {
                        results.AddRange(found);
                        break;
                    }
                }
                catch { }
            }
            return results;
        }

        public List<FunctionABI> FindFunctionABIFromInputData(string inputData)
        {
            var results = new List<FunctionABI>();

            var cached = _cache.FindFunctionABIFromInputData(inputData);
            if (cached != null && cached.Count > 0)
                results.AddRange(cached);

            foreach (var impl in _implementations)
            {
                try
                {
                    var found = impl.FindFunctionABIFromInputData(inputData);
                    if (found != null && found.Count > 0)
                    {
                        results.AddRange(found);
                        break;
                    }
                }
                catch { }
            }
            return results;
        }

        public List<EventABI> FindEventABI(string signature)
        {
            var results = new List<EventABI>();

            var cached = _cache.FindEventABI(signature);
            if (cached != null && cached.Count > 0)
                results.AddRange(cached);

            foreach (var impl in _implementations)
            {
                try
                {
                    var found = impl.FindEventABI(signature);
                    if (found != null && found.Count > 0)
                    {
                        results.AddRange(found);
                        break;
                    }
                }
                catch { }
            }
            return results;
        }

        public List<ErrorABI> FindErrorABI(string signature)
        {
            var results = new List<ErrorABI>();

            var cached = _cache.FindErrorABI(signature);
            if (cached != null && cached.Count > 0)
                results.AddRange(cached);

            foreach (var impl in _implementations)
            {
                try
                {
                    var found = impl.FindErrorABI(signature);
                    if (found != null && found.Count > 0)
                    {
                        results.AddRange(found);
                        break;
                    }
                }
                catch { }
            }
            return results;
        }

        public async Task<FunctionABI> FindFunctionABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = await GetABIInfoAsync((long)chainId, contractAddress).ConfigureAwait(false);
            var result = abiInfo?.ContractABI?.FindFunctionABI(signature);
            if (result != null) return result;

            return FindInImplementations(chainId, contractAddress,
                (cid, addr) => _cache.FindFunctionABI(cid, addr, signature));
        }

        public async Task<FunctionABI> FindFunctionABIFromInputDataAsync(BigInteger chainId, string contractAddress, string inputData)
        {
            var abiInfo = await GetABIInfoAsync((long)chainId, contractAddress).ConfigureAwait(false);
            var result = abiInfo?.ContractABI?.FindFunctionABIFromInputData(inputData);
            if (result != null) return result;

            return FindInImplementations(chainId, contractAddress,
                (cid, addr) => _cache.FindFunctionABIFromInputData(cid, addr, inputData));
        }

        public async Task<EventABI> FindEventABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = await GetABIInfoAsync((long)chainId, contractAddress).ConfigureAwait(false);
            var result = abiInfo?.ContractABI?.FindEventABI(signature);
            if (result != null) return result;

            return FindInImplementations(chainId, contractAddress,
                (cid, addr) => _cache.FindEventABI(cid, addr, signature));
        }

        public async Task<ErrorABI> FindErrorABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = await GetABIInfoAsync((long)chainId, contractAddress).ConfigureAwait(false);
            var result = abiInfo?.ContractABI?.FindErrorABI(signature);
            if (result != null) return result;

            return FindInImplementations(chainId, contractAddress,
                (cid, addr) => _cache.FindErrorABI(cid, addr, signature));
        }

        public async Task<IDictionary<string, FunctionABI>> FindFunctionABIsBatchAsync(IEnumerable<string> signatures)
        {
            var result = new Dictionary<string, FunctionABI>();
            var remaining = new HashSet<string>(signatures);

            var cached = await _cache.FindFunctionABIsBatchAsync(remaining).ConfigureAwait(false);
            foreach (var kvp in cached)
            {
                result[kvp.Key] = kvp.Value;
                remaining.Remove(kvp.Key);
            }

            foreach (var impl in _implementations)
            {
                if (remaining.Count == 0) break;
                try
                {
                    var found = await impl.FindFunctionABIsBatchAsync(remaining).ConfigureAwait(false);
                    foreach (var kvp in found)
                    {
                        if (!result.ContainsKey(kvp.Key))
                        {
                            result[kvp.Key] = kvp.Value;
                            remaining.Remove(kvp.Key);
                        }
                    }
                }
                catch { }
            }
            return result;
        }

        public async Task<IDictionary<string, EventABI>> FindEventABIsBatchAsync(IEnumerable<string> signatures)
        {
            var result = new Dictionary<string, EventABI>();
            var remaining = new HashSet<string>(signatures);

            var cached = await _cache.FindEventABIsBatchAsync(remaining).ConfigureAwait(false);
            foreach (var kvp in cached)
            {
                result[kvp.Key] = kvp.Value;
                remaining.Remove(kvp.Key);
            }

            foreach (var impl in _implementations)
            {
                if (remaining.Count == 0) break;
                try
                {
                    var found = await impl.FindEventABIsBatchAsync(remaining).ConfigureAwait(false);
                    foreach (var kvp in found)
                    {
                        if (!result.ContainsKey(kvp.Key))
                        {
                            result[kvp.Key] = kvp.Value;
                            remaining.Remove(kvp.Key);
                        }
                    }
                }
                catch { }
            }
            return result;
        }

        public async Task<ABIBatchResult> FindABIsBatchAsync(IEnumerable<string> functionSignatures, IEnumerable<string> eventSignatures)
        {
            var result = new ABIBatchResult();
            var remainingFunctions = new HashSet<string>(functionSignatures ?? Enumerable.Empty<string>());
            var remainingEvents = new HashSet<string>(eventSignatures ?? Enumerable.Empty<string>());

            var cached = await _cache.FindABIsBatchAsync(remainingFunctions, remainingEvents).ConfigureAwait(false);
            foreach (var kvp in cached.Functions)
            {
                result.Functions[kvp.Key] = kvp.Value;
                remainingFunctions.Remove(kvp.Key);
            }
            foreach (var kvp in cached.Events)
            {
                result.Events[kvp.Key] = kvp.Value;
                remainingEvents.Remove(kvp.Key);
            }

            foreach (var impl in _implementations)
            {
                if (remainingFunctions.Count == 0 && remainingEvents.Count == 0) break;

                try
                {
                    var found = await impl.FindABIsBatchAsync(remainingFunctions, remainingEvents).ConfigureAwait(false);

                    foreach (var kvp in found.Functions)
                    {
                        if (!result.Functions.ContainsKey(kvp.Key))
                        {
                            result.Functions[kvp.Key] = kvp.Value;
                            remainingFunctions.Remove(kvp.Key);
                        }
                    }

                    foreach (var kvp in found.Events)
                    {
                        if (!result.Events.ContainsKey(kvp.Key))
                        {
                            result.Events[kvp.Key] = kvp.Value;
                            remainingEvents.Remove(kvp.Key);
                        }
                    }
                }
                catch { }
            }
            return result;
        }

        private T FindInImplementations<T>(BigInteger chainId, string contractAddress, Func<BigInteger, string, T> finder) where T : class
        {
            var key = GetCacheKey((long)chainId, contractAddress?.ToLowerInvariant());
            if (_proxyToImplementations.TryGetValue(key, out var implementations))
            {
                foreach (var implAddress in implementations)
                {
                    var result = finder(chainId, implAddress);
                    if (result != null) return result;
                }
            }
            return null;
        }

        private static string GetCacheKey(long chainId, string address)
        {
            return $"{chainId}:{address?.ToLowerInvariant()}";
        }
    }
}
