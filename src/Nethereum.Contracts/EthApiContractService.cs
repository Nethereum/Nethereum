using Nethereum.Contracts.CQS;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.TransactionManagers;

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

        public Contract GetContract<TContractMessage>(string contractAddress)
        {
            var contract = new Contract(this, typeof(TContractMessage), contractAddress)
            {
                DefaultBlock = this.DefaultBlock
            };
            return contract;
        }

        //public ContractDeploymentHandler<TContractDeploymentMessage> GetContractDeploymentHandler<TContractDeploymentMessage>()
        //{
        //     return new ContractDeploymentHandler<TContractDeploymentMessage>(this.Client, )
        //}

        public EthApiContractService(IClient client) : base(client)
        {
        }

        public EthApiContractService(IClient client, ITransactionManager transactionManager) : base(client, transactionManager)
        {
        }
    }
}