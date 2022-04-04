using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Contracts.Standards.ERC1155;
using Nethereum.Contracts.Standards.ERC20;
using Nethereum.Contracts.Standards.ERC721;
using Nethereum.RPC;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Contracts.Services
{
    public interface IEthApiContractService: IEthApiService
    {
        IDeployContract DeployContract { get; }
        Contract GetContract(string abi, string contractAddress);
        Contract GetContract<TContractMessage>(string contractAddress);
        Event<TEventType> GetEvent<TEventType>() where TEventType : IEventDTO, new();
        Event<TEventType> GetEvent<TEventType>(string contractAddress) where TEventType : IEventDTO, new();

#if !DOTNET35
        IContractDeploymentTransactionHandler<TContractDeploymentMessage> GetContractDeploymentHandler<TContractDeploymentMessage>() where TContractDeploymentMessage : ContractDeploymentMessage, new();
        ContractHandler GetContractHandler(string contractAddress);
        IContractQueryHandler<TContractFunctionMessage> GetContractQueryHandler<TContractFunctionMessage>() where TContractFunctionMessage : FunctionMessage, new();

        /// <summary>
        /// Creates a multi query handler, to enable execute a single request combining multiple queries to multiple contracts using the multicall contract https://github.com/makerdao/multicall/blob/master/src/Multicall.sol
        /// This is deployed at https://etherscan.io/address/0xeefBa1e63905eF1D7ACbA5a8513c70307C1cE441#code
        /// </summary>
        /// <param name="multiContractAdress">The address of the deployed multicall contract</param>
        MultiQueryHandler GetMultiQueryHandler(string multiContractAdress = CommonAddresses.MULTICALL_ADDRESS);

        ERC20Service GetERC20Service(string contractAddress);
        IContractTransactionHandler<TContractFunctionMessage> GetContractTransactionHandler<TContractFunctionMessage>() where TContractFunctionMessage : FunctionMessage, new();
        IEthGetContractTransactionErrorReason GetContractTransactionErrorReason { get; }
        ERC721Service GetERC721Service(string contractAddress);
        ERC1155Service GetERC1155Service(string contractAddress);
        ENSService GetEnsService(string ensRegistryAddress = CommonAddresses.ENS_REGISTRY_ADDRESS);
        EthTLSService GetEnsEthTlsService(string ensRegistryAddress = CommonAddresses.ENS_REGISTRY_ADDRESS);
#endif


    }
}