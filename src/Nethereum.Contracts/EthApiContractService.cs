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

#if !DOTNET35
        public ContractDeploymentHandler<TContractDeploymentMessage> GetContractDeploymentHandler<TContractDeploymentMessage>() 
            where TContractDeploymentMessage: ContractDeploymentMessage
        {
            var contractDeploymentHandler = new ContractDeploymentHandler<TContractDeploymentMessage>();
            contractDeploymentHandler.Initialise(this);
            return contractDeploymentHandler;
        }

        public ContractTransactionHandler<TContractFunctionMessage> GetContractTrasactionHandler<TContractFunctionMessage>() 
                   where TContractFunctionMessage : ContractMessage
        {
            var contractTransactionHandler = new ContractTransactionHandler<TContractFunctionMessage>();
            contractTransactionHandler.Initialise(this);
            return contractTransactionHandler;
        }

        public ContractQueryHandler<TContractFunctionMessage> GetContractQueryHandler<TContractFunctionMessage>() 
                   where TContractFunctionMessage : ContractMessage
        {
            var contractQueryHandler = new ContractQueryHandler<TContractFunctionMessage>();
            contractQueryHandler.Initialise(this);
            return contractQueryHandler;
        }
#endif

        public EthApiContractService(IClient client) : base(client)
        {
        }

        public EthApiContractService(IClient client, ITransactionManager transactionManager) : base(client, transactionManager)
        {
        }
    }
}