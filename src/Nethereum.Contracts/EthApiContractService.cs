using Nethereum.JsonRpc.Client;
using Nethereum.RPC;

namespace Nethereum.Contracts
{
    public class EthApiContractService : EthApiService
    {
        public DeployContract DeployContract => new DeployContract(this.TransactionManager);

        public Contract GetContract(string abi, string contractAddress)
        {
            var contract = new Contract(this, abi, contractAddress)
            {
                DefaultBlock = this.DefaultBlock
            };
            return contract;
        }

        public EthApiContractService(IClient client) : base(client)
        {
        }
    }
}