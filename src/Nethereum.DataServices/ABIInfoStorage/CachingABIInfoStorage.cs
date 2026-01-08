using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.Model;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.DataServices.ABIInfoStorage
{
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
    }
}
