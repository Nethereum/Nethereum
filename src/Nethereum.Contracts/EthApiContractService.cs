using Nethereum.Contracts.CQS;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts
{
    public class EthApiContractService : EthApiService
    {
        public EthApiContractService(IClient client) : base(client)
        {
        }

        public EthApiContractService(IClient client, ITransactionManager transactionManager) : base(client,
            transactionManager)
        {
        }

        public DeployContract DeployContract => new DeployContract(TransactionManager);

        public Contract GetContract(string abi, string contractAddress)
        {
            var contract = new Contract(this, abi, contractAddress)
            {
                DefaultBlock = DefaultBlock
            };
            return contract;
        }

        public Contract GetContract<TContractMessage>(string contractAddress)
        {
            var contract = new Contract(this, typeof(TContractMessage), contractAddress)
            {
                DefaultBlock = DefaultBlock
            };
            return contract;
        }

#if !DOTNET35

        public ContractHandler GetContractHandler(string contractAddress)
        {
            string address = null;
            if (TransactionManager != null)
                if (TransactionManager.Account != null)
                    address = TransactionManager.Account.Address;
            return new ContractHandler(contractAddress, this, address);
        }

        public ContractDeploymentHandler<TContractDeploymentMessage> GetContractDeploymentHandler<
            TContractDeploymentMessage>()
            where TContractDeploymentMessage : ContractDeploymentMessage, new()
        {
            var contractDeploymentHandler = new ContractDeploymentHandler<TContractDeploymentMessage>();
            contractDeploymentHandler.Initialise(this);
            return contractDeploymentHandler;
        }

        public ContractTransactionHandler<TContractFunctionMessage> GetContractTransactionHandler<
            TContractFunctionMessage>()
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
    }
}