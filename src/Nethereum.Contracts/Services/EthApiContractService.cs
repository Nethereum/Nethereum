using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.CQS;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts.Services
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
            return new Contract(this, abi, contractAddress);
        }

        public Contract GetContract<TContractMessage>(string contractAddress)
        {
           return new Contract(this, typeof(TContractMessage), contractAddress);
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

        public IContractDeploymentTransactionHandler<TContractDeploymentMessage> GetContractDeploymentHandler<
            TContractDeploymentMessage>()
            where TContractDeploymentMessage : ContractDeploymentMessage, new()
        {
            return new ContractDeploymentTransactionHandler<TContractDeploymentMessage>(this.TransactionManager);
        }

        public IContractTransactionHandler<TContractFunctionMessage> GetContractTransactionHandler<
            TContractFunctionMessage>()
            where TContractFunctionMessage : FunctionMessage, new()
        {
            return new ContractTransactionHandler<TContractFunctionMessage>(this.TransactionManager);
        }

        public IContractQueryHandler<TContractFunctionMessage> GetContractQueryHandler<TContractFunctionMessage>()
            where TContractFunctionMessage : FunctionMessage, new()
        {
            return new ContractQueryEthCallHandler<TContractFunctionMessage>(Transactions.Call, 
                TransactionManager?.Account?.Address, DefaultBlock);
        }
#endif
    }
}