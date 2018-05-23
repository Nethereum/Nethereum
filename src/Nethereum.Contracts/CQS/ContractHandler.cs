using System.Threading;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{

    public class ContractHandler
    {
        public ContractHandler(string contractAddress, EthApiContractService ethApiContractService,
            string addressFrom = null)
        {
            ContractAddress = contractAddress;
            EthApiContractService = ethApiContractService;
            AddressFrom = addressFrom;
        }

        protected string AddressFrom { get; set; }

        public string ContractAddress { get; }
        public EthApiContractService EthApiContractService { get; }

        public Event<TEventType> GetEvent<TEventType>() where TEventType : new()
        {
            if (!EventAttribute.IsEventType(typeof(TEventType))) return null;
            var attribute = EventAttribute.GetAttribute<TEventType>();
            var contract = new Contract(EthApiContractService, typeof(TEventType), ContractAddress);
            return contract.GetEvent<TEventType>(attribute.Name);
        }

        protected void SetAddressFrom(ContractMessage contractMessage)
        {
            contractMessage.FromAddress = contractMessage.FromAddress ?? AddressFrom;
        }

#if !DOTNET35
        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(
             CancellationTokenSource tokenSource = null)
            where TEthereumContractFunctionMessage : ContractMessage, new()
        {
            var message = new TEthereumContractFunctionMessage();
            return SendRequestAndWaitForReceiptAsync(message, tokenSource);
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMesssage, CancellationTokenSource tokenSource = null)
            where TEthereumContractFunctionMessage : ContractMessage
        {
            var command = EthApiContractService.GetContractTransactionHandler<TEthereumContractFunctionMessage>();
            SetAddressFrom(transactionMesssage);
            return command.SendRequestAndWaitForReceiptAsync(transactionMesssage, ContractAddress, tokenSource);
        }

        public Task<string> SendRequestAsync<TEthereumContractFunctionMessage>()
            where TEthereumContractFunctionMessage : ContractMessage, new()
        {
            var message = new TEthereumContractFunctionMessage();
            return SendRequestAsync(message);
        }

        public Task<string> SendRequestAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMesssage)
            where TEthereumContractFunctionMessage : ContractMessage
        {
            var command = EthApiContractService.GetContractTransactionHandler<TEthereumContractFunctionMessage>();
            SetAddressFrom(transactionMesssage);
            return command.SendRequestAsync(transactionMesssage, ContractAddress);
        }

        public Task<HexBigInteger> EstimateGasAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMesssage)
            where TEthereumContractFunctionMessage : ContractMessage
        {
            var command = EthApiContractService.GetContractTransactionHandler<TEthereumContractFunctionMessage>();
            SetAddressFrom(transactionMesssage);
            return command.EstimateGasAsync(transactionMesssage, ContractAddress);
        }

        public Task<TransactionReceipt> SendDeploymentRequestAndWaitForReceiptAsync<TEthereumContractDeploymentMessage>(
            TEthereumContractDeploymentMessage ethereumDeploymentMessage, CancellationTokenSource tokenSource = null)
            where TEthereumContractDeploymentMessage : ContractDeploymentMessage, new()
        {
            SetAddressFrom(ethereumDeploymentMessage);
            var deploymentHandler =
                EthApiContractService.GetContractDeploymentHandler<TEthereumContractDeploymentMessage>();
            return deploymentHandler.SendRequestAndWaitForReceiptAsync(ethereumDeploymentMessage, tokenSource);
        }

        public Task<string> SendDeploymentRequestAsync<TEthereumContractDeploymentMessage>(
            TEthereumContractDeploymentMessage ethereumDeploymentMessage)
            where TEthereumContractDeploymentMessage : ContractDeploymentMessage, new()
        {
            SetAddressFrom(ethereumDeploymentMessage);
            var deploymentHandler =
                EthApiContractService.GetContractDeploymentHandler<TEthereumContractDeploymentMessage>();
            return deploymentHandler.SendRequestAsync(ethereumDeploymentMessage);
        }

        public Task<HexBigInteger> EstimateDeploymentGasAsync<TEthereumContractDeploymentMessage>(
            TEthereumContractDeploymentMessage ethereumDeploymentMessage)
            where TEthereumContractDeploymentMessage : ContractDeploymentMessage, new()
        {
            var command = EthApiContractService.GetContractDeploymentHandler<TEthereumContractDeploymentMessage>();
            SetAddressFrom(ethereumDeploymentMessage);
            return command.EstimateGasAsync(ethereumDeploymentMessage);
        }

        public Task<TEthereumFunctionReturn> QueryDeserializingToObjectAsync<TEthereumContractFunctionMessage,
            TEthereumFunctionReturn>(TEthereumContractFunctionMessage ethereumContractFunctionMessage,
            BlockParameter blockParameter = null)
            where TEthereumContractFunctionMessage : ContractMessage
            where TEthereumFunctionReturn : new()
        {
            SetAddressFrom(ethereumContractFunctionMessage);
            var queryHandler = EthApiContractService.GetContractQueryHandler<TEthereumContractFunctionMessage>();
            return queryHandler.QueryDeserializingToObjectAsync<TEthereumFunctionReturn>(
                ethereumContractFunctionMessage,
                ContractAddress, blockParameter);
        }

        public Task<TEthereumFunctionReturn> QueryDeserializingToObjectAsync<TEthereumContractFunctionMessage,
            TEthereumFunctionReturn>(
            BlockParameter blockParameter = null)
            where TEthereumContractFunctionMessage : ContractMessage, new()
            where TEthereumFunctionReturn : new()
        {
            var ethereumContractFunctionMessage = new TEthereumContractFunctionMessage();
            return QueryDeserializingToObjectAsync<TEthereumContractFunctionMessage, TEthereumFunctionReturn>(
                ethereumContractFunctionMessage, blockParameter);
        }

        public Task<TReturn> QueryAsync<TEthereumContractFunctionMessage, TReturn>(
            TEthereumContractFunctionMessage ethereumContractFunctionMessage, BlockParameter blockParameter = null)
            where TEthereumContractFunctionMessage : ContractMessage
        {
            SetAddressFrom(ethereumContractFunctionMessage);
            var queryHandler = EthApiContractService.GetContractQueryHandler<TEthereumContractFunctionMessage>();
            return queryHandler.QueryAsync<TReturn>(ethereumContractFunctionMessage,
                ContractAddress, blockParameter);
        }

        public Task<TReturn> QueryAsync<TEthereumContractFunctionMessage, TReturn>(BlockParameter blockParameter = null)
            where TEthereumContractFunctionMessage : ContractMessage, new()
        {
            var ethereumContractFunctionMessage = new TEthereumContractFunctionMessage();
            return QueryAsync<TEthereumContractFunctionMessage, TReturn>(ethereumContractFunctionMessage,
                blockParameter);
        }
#endif
        
    }

}