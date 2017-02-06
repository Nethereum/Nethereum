using Nethereum.RPC.Eth.Services;

namespace Nethereum.Web3
{
    public static class EthApiContractExtensions
    {
        public static DeployContract GetDeployContract(this EthApiService apiService)
        {
            return new DeployContract(apiService.TransactionManager);
        }
        public static Contract GetContract(this EthApiService apiService, string abi, string contractAddress)
        {
            var contract = new Contract(apiService, abi, contractAddress)
            {
                DefaultBlock = apiService.DefaultBlock
            };
            return contract;
        }
    }
}