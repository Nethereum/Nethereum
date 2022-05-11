using System;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Contracts.Standards.ERC1155;
using Nethereum.Contracts.Standards.ERC1271;
using Nethereum.Contracts.Standards.ERC20;
using Nethereum.Contracts.Standards.ERC721;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts.Services
{
    public class EthApiContractService : EthApiService, IEthApiContractService
    {
        public EthApiContractService(IClient client) : base(client)
        {
#if !DOTNET35
            GetContractTransactionErrorReason = new EthGetContractTransactionErrorReason(Transactions);
            ERC721 = new ERC721Service(this);
            ERC20 = new ERC20Service(this);
            ERC1155 = new ERC1155Service(this);
            ERC1271 = new ERC1271Service(this);
#endif
        }

        public EthApiContractService(IClient client, ITransactionManager transactionManager) : base(client,
            transactionManager)
        {
#if !DOTNET35
            GetContractTransactionErrorReason = new EthGetContractTransactionErrorReason(Transactions);
#endif 
        }

        public IDeployContract DeployContract => new DeployContract(TransactionManager);

        public Contract GetContract(string abi, string contractAddress)
        {
            return new Contract(this, abi, contractAddress);
        }

        public Contract GetContract<TContractMessage>(string contractAddress)
        {
           return new Contract(this, typeof(TContractMessage), contractAddress);
        }

        public Event<TEventType> GetEvent<TEventType>() where TEventType : IEventDTO, new()
        {
            if (!EventAttribute.IsEventType(typeof(TEventType))) throw new ArgumentException("The type given is not a valid Event"); ;
            return new Event<TEventType>(Client);
        }

        public Event<TEventType> GetEvent<TEventType>(string contractAddress) where TEventType : IEventDTO, new()
        {
            if (!EventAttribute.IsEventType(typeof(TEventType))) throw new ArgumentException("The type given is not a valid Event");
            return new Event<TEventType>(Client, contractAddress);
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

        /// <summary>
        /// Multicall using the contract https://github.com/makerdao/multicall/blob/master/src/Multicall.sol
        /// </summary>
        /// <param name="multiContractAdress">The contracts address of the deployed contract</param>
        /// <returns></returns>
        public MultiQueryHandler GetMultiQueryHandler(string multiContractAdress = CommonAddresses.MULTICALL_ADDRESS)
        {
            return new MultiQueryHandler(Client, multiContractAdress, TransactionManager?.Account?.Address,
                DefaultBlock);
        }

        /// <summary>
        /// ERC20 Standard Token Service to interact with smart contracts compliant with the standard interface
        /// https://ethereum.org/en/developers/docs/standards/tokens/erc-20/
        /// </summary>
        public ERC20Service ERC20 { get; private set; }

        /// <summary>
        /// ERC721 NFT - Non Fungible Token Standard Service to interact with smart contracts compliant with the standard interface
        /// https://ethereum.org/en/developers/docs/standards/tokens/erc-721
        /// </summary>
        public ERC721Service ERC721 { get; private set; }

        /// <summary>
        /// ERC1155 Multi token standard Service to interact with smart contracts compliant with the standard interface
        /// https://ethereum.org/en/developers/docs/standards/tokens/erc-1155/
        /// </summary>
        public ERC1155Service ERC1155 { get; private set; }

        /// <summary>
        /// ERC1271: Standard Signature Validation Method for Contracts, Service to interact with smart contracts compliant with the standard interface
        /// This enables to validate if a signature is valid for a smart contract
        /// https://eips.ethereum.org/EIPS/eip-1271
        /// </summary>
        public ERC1271Service ERC1271 { get; private set; }

        public ENSService GetEnsService(string ensRegistryAddress = CommonAddresses.ENS_REGISTRY_ADDRESS)
        {
            return new ENSService(this, ensRegistryAddress);
        }

        public EthTLSService GetEnsEthTlsService(string ensRegistryAddress = CommonAddresses.ENS_REGISTRY_ADDRESS)
        {
            return new EthTLSService(this, ensRegistryAddress);
        }

        public IEthGetContractTransactionErrorReason GetContractTransactionErrorReason { get; }

        public IContractQueryHandler<TContractFunctionMessage> GetContractQueryHandler<TContractFunctionMessage>()
            where TContractFunctionMessage : FunctionMessage, new()
        {
            return new ContractQueryEthCallHandler<TContractFunctionMessage>(Transactions.Call, 
                TransactionManager?.Account?.Address, DefaultBlock);
        }
#endif
    }
}