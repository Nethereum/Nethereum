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

        public EtherscanABIInfoStorage(string apiKey = null)
        {
            _apiKey = apiKey ?? EtherscanRequestService.DefaultToken;
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

            return await FetchFromEtherscanAsync(chainId, contractAddress).ConfigureAwait(false);
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
