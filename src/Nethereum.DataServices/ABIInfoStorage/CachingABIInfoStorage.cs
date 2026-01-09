using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.Model;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.DataServices.ABIInfoStorage
{
    [Obsolete("Use CompositeABIInfoStorage with an injectable cache instead. CompositeABIInfoStorage now handles caching internally.")]
    public class CachingABIInfoStorage : IABIInfoStorage
    {
        private readonly IABIInfoStorage _inner;
        private readonly ABIInfoInMemoryStorage _cache;

        public CachingABIInfoStorage(IABIInfoStorage inner, ABIInfoInMemoryStorage cache = null)
        {
            _inner = inner;
            _cache = cache ?? new ABIInfoInMemoryStorage();
        }

        public void AddABIInfo(ABIInfo abiInfo)
        {
            _cache.AddABIInfo(abiInfo);
        }

        public ABIInfo GetABIInfo(BigInteger chainId, string contractAddress)
        {
            var cached = _cache.GetABIInfo(chainId, contractAddress);
            if (cached != null) return cached;

            var result = _inner.GetABIInfo(chainId, contractAddress);
            if (result != null)
            {
                _cache.AddABIInfo(result);
            }
            return result;
        }

        public FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature)
        {
            var cached = _cache.FindFunctionABI(chainId, contractAddress, signature);
            if (cached != null) return cached;

            var result = _inner.FindFunctionABI(chainId, contractAddress, signature);
            if (result != null)
            {
                var abiInfo = _inner.GetABIInfo(chainId, contractAddress);
                if (abiInfo != null && _cache.GetABIInfo(chainId, contractAddress) == null)
                {
                    _cache.AddABIInfo(abiInfo);
                }
            }
            return result;
        }

        public FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData)
        {
            var cached = _cache.FindFunctionABIFromInputData(chainId, contractAddress, inputData);
            if (cached != null) return cached;

            var result = _inner.FindFunctionABIFromInputData(chainId, contractAddress, inputData);
            if (result != null)
            {
                var abiInfo = _inner.GetABIInfo(chainId, contractAddress);
                if (abiInfo != null && _cache.GetABIInfo(chainId, contractAddress) == null)
                {
                    _cache.AddABIInfo(abiInfo);
                }
            }
            return result;
        }

        public EventABI FindEventABI(BigInteger chainId, string contractAddress, string signature)
        {
            var cached = _cache.FindEventABI(chainId, contractAddress, signature);
            if (cached != null) return cached;

            var result = _inner.FindEventABI(chainId, contractAddress, signature);
            if (result != null)
            {
                var abiInfo = _inner.GetABIInfo(chainId, contractAddress);
                if (abiInfo != null && _cache.GetABIInfo(chainId, contractAddress) == null)
                {
                    _cache.AddABIInfo(abiInfo);
                }
            }
            return result;
        }

        public ErrorABI FindErrorABI(BigInteger chainId, string contractAddress, string signature)
        {
            var cached = _cache.FindErrorABI(chainId, contractAddress, signature);
            if (cached != null) return cached;

            var result = _inner.FindErrorABI(chainId, contractAddress, signature);
            if (result != null)
            {
                var abiInfo = _inner.GetABIInfo(chainId, contractAddress);
                if (abiInfo != null && _cache.GetABIInfo(chainId, contractAddress) == null)
                {
                    _cache.AddABIInfo(abiInfo);
                }
            }
            return result;
        }

        public List<FunctionABI> FindFunctionABI(string signature)
        {
            var cached = _cache.FindFunctionABI(signature);
            if (cached != null && cached.Count > 0) return cached;
            return _inner.FindFunctionABI(signature);
        }

        public List<FunctionABI> FindFunctionABIFromInputData(string inputData)
        {
            var cached = _cache.FindFunctionABIFromInputData(inputData);
            if (cached != null && cached.Count > 0) return cached;
            return _inner.FindFunctionABIFromInputData(inputData);
        }

        public List<EventABI> FindEventABI(string signature)
        {
            var cached = _cache.FindEventABI(signature);
            if (cached != null && cached.Count > 0) return cached;
            return _inner.FindEventABI(signature);
        }

        public List<ErrorABI> FindErrorABI(string signature)
        {
            var cached = _cache.FindErrorABI(signature);
            if (cached != null && cached.Count > 0) return cached;
            return _inner.FindErrorABI(signature);
        }

        public async Task<ABIInfo> GetABIInfoAsync(long chainId, string contractAddress)
        {
            var cached = await _cache.GetABIInfoAsync(chainId, contractAddress).ConfigureAwait(false);
            if (cached != null) return cached;

            var result = await _inner.GetABIInfoAsync(chainId, contractAddress).ConfigureAwait(false);
            if (result != null)
            {
                _cache.AddABIInfo(result);
            }
            return result;
        }

        public async Task<FunctionABI> FindFunctionABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            var cached = _cache.FindFunctionABI(chainId, contractAddress, signature);
            if (cached != null) return cached;

            return await _inner.FindFunctionABIAsync(chainId, contractAddress, signature).ConfigureAwait(false);
        }

        public async Task<FunctionABI> FindFunctionABIFromInputDataAsync(BigInteger chainId, string contractAddress, string inputData)
        {
            var cached = _cache.FindFunctionABIFromInputData(chainId, contractAddress, inputData);
            if (cached != null) return cached;

            return await _inner.FindFunctionABIFromInputDataAsync(chainId, contractAddress, inputData).ConfigureAwait(false);
        }

        public async Task<EventABI> FindEventABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            var cached = _cache.FindEventABI(chainId, contractAddress, signature);
            if (cached != null) return cached;

            return await _inner.FindEventABIAsync(chainId, contractAddress, signature).ConfigureAwait(false);
        }

        public async Task<ErrorABI> FindErrorABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            var cached = _cache.FindErrorABI(chainId, contractAddress, signature);
            if (cached != null) return cached;

            return await _inner.FindErrorABIAsync(chainId, contractAddress, signature).ConfigureAwait(false);
        }

        public Task<IDictionary<string, FunctionABI>> FindFunctionABIsBatchAsync(IEnumerable<string> signatures)
        {
            return _inner.FindFunctionABIsBatchAsync(signatures);
        }

        public Task<IDictionary<string, EventABI>> FindEventABIsBatchAsync(IEnumerable<string> signatures)
        {
            return _inner.FindEventABIsBatchAsync(signatures);
        }

        public Task<ABIBatchResult> FindABIsBatchAsync(IEnumerable<string> functionSignatures, IEnumerable<string> eventSignatures)
        {
            return _inner.FindABIsBatchAsync(functionSignatures, eventSignatures);
        }
    }
}
