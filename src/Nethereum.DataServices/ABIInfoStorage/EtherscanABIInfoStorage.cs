using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.Model;
using Nethereum.DataServices.Etherscan;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.DataServices.ABIInfoStorage
{
    public class EtherscanABIInfoStorage : IABIInfoStorage
    {
        private readonly string _apiKey;
        private readonly ABIInfoInMemoryStorage _cache = new ABIInfoInMemoryStorage();

        public EtherscanABIInfoStorage(string apiKey = null)
        {
            _apiKey = apiKey ?? EtherscanRequestService.DefaultToken;
        }

        public void AddABIInfo(ABIInfo abiInfo)
        {
            _cache.AddABIInfo(abiInfo);
        }

        public ABIInfo GetABIInfo(BigInteger chainId, string contractAddress)
        {
            var cached = _cache.GetABIInfo(chainId, contractAddress);
            if (cached != null) return cached;

            var abiInfo = FetchFromEtherscanAsync((long)chainId, contractAddress).ConfigureAwait(false).GetAwaiter().GetResult();
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

            var abiInfo = await FetchFromEtherscanAsync(chainId, contractAddress).ConfigureAwait(false);
            if (abiInfo != null)
            {
                _cache.AddABIInfo(abiInfo);
            }
            return abiInfo;
        }

        public FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature)
        {
            EnsureLoaded(chainId, contractAddress);
            return _cache.FindFunctionABI(chainId, contractAddress, signature);
        }

        public FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData)
        {
            EnsureLoaded(chainId, contractAddress);
            return _cache.FindFunctionABIFromInputData(chainId, contractAddress, inputData);
        }

        public EventABI FindEventABI(BigInteger chainId, string contractAddress, string signature)
        {
            EnsureLoaded(chainId, contractAddress);
            return _cache.FindEventABI(chainId, contractAddress, signature);
        }

        public ErrorABI FindErrorABI(BigInteger chainId, string contractAddress, string signature)
        {
            EnsureLoaded(chainId, contractAddress);
            return _cache.FindErrorABI(chainId, contractAddress, signature);
        }

        public List<FunctionABI> FindFunctionABI(string signature)
        {
            return _cache.FindFunctionABI(signature);
        }

        public List<FunctionABI> FindFunctionABIFromInputData(string inputData)
        {
            return _cache.FindFunctionABIFromInputData(inputData);
        }

        public List<EventABI> FindEventABI(string signature)
        {
            return _cache.FindEventABI(signature);
        }

        public List<ErrorABI> FindErrorABI(string signature)
        {
            return _cache.FindErrorABI(signature);
        }

        private void EnsureLoaded(BigInteger chainId, string contractAddress)
        {
            if (_cache.GetABIInfo(chainId, contractAddress) == null)
            {
                var abiInfo = FetchFromEtherscanAsync((long)chainId, contractAddress).ConfigureAwait(false).GetAwaiter().GetResult();
                if (abiInfo != null)
                {
                    _cache.AddABIInfo(abiInfo);
                }
            }
        }

        private async Task<ABIInfo> FetchFromEtherscanAsync(long chainId, string address)
        {
            try
            {
                var etherscan = new EtherscanApiService(chainId, _apiKey);
                var response = await etherscan.Contracts.GetAbiAsync(address).ConfigureAwait(false);

                if (response?.Result == null || response.Status != "1") return null;

                var abiString = response.Result;
                if (string.IsNullOrEmpty(abiString) || abiString == "Contract source code not verified") return null;

                return ABIInfo.FromABI(
                    abiString,
                    address?.ToLowerInvariant(),
                    null,
                    null,
                    chainId);
            }
            catch
            {
                return null;
            }
        }
    }
}
