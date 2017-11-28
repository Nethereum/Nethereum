using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{
#if !DOTNET35
    public class ContractHandler
    {
        public ContractHandler(string contractAddress, EthApiContractService ethApiContractService, string addressFrom = null)
        {
            ContractAddress = contractAddress;
            EthApiContractService = ethApiContractService;
            AddressFrom = addressFrom;
        }

        protected string AddressFrom { get; set; }

        public string ContractAddress { get; }
        public EthApiContractService EthApiContractService { get; }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(TEthereumContractFunctionMessage transferMessage, CancellationTokenSource tokenSource = null) where TEthereumContractFunctionMessage : ContractMessage
        {
            var command = EthApiContractService.GetContractTrasactionHandler<TEthereumContractFunctionMessage>();
            SetAddressFrom(transferMessage);
            return command.SendRequestAndWaitForReceiptAsync(transferMessage, ContractAddress, tokenSource);
        }

        public Task<TransactionReceipt> SendRequestAsync<TEthereumContractFunctionMessage>(TEthereumContractFunctionMessage transferMessage, CancellationTokenSource tokenSource = null) where TEthereumContractFunctionMessage : ContractMessage
        {
            var command = EthApiContractService.GetContractTrasactionHandler<TEthereumContractFunctionMessage>();
            SetAddressFrom(transferMessage);
            return command.SendRequestAsync(transferMessage, ContractAddress);
        }


        public Task<TransactionReceipt> SendDeploymentRequestAndWaitForReceiptAsync<TEthereumContractDeploymentMessage>(TEthereumContractDeploymentMessage ethereumDeploymentMessage, CancellationTokenSource tokenSource = null)
            where TEthereumContractDeploymentMessage : ContractDeploymentMessage
        {
            SetAddressFrom(ethereumDeploymentMessage);
            var deploymentHandler = EthApiContractService.GetContractDeploymentHandler<TEthereumContractDeploymentMessage>();
            return deploymentHandler.SendRequestAndWaitForReceiptAsync(ethereumDeploymentMessage, tokenSource);
        }

        public Task<string> SendDeploymentRequestAsync<TEthereumContractDeploymentMessage>(TEthereumContractDeploymentMessage ethereumDeploymentMessage)
            where TEthereumContractDeploymentMessage : ContractDeploymentMessage
        {
            SetAddressFrom(ethereumDeploymentMessage);
            var deploymentHandler = EthApiContractService.GetContractDeploymentHandler<TEthereumContractDeploymentMessage>();
            return deploymentHandler.SendRequestAsync(ethereumDeploymentMessage);
        }

        public Task<TEthereumFunctionReturn> QueryDeserializingToObjectAsync<TEthereumContractFunctionMessage, TEthereumFunctionReturn>(TEthereumContractFunctionMessage ethereumContractFunctionMessage, BlockParameter blockParameter = null)
            where TEthereumContractFunctionMessage : ContractMessage
            where TEthereumFunctionReturn : new()
        {
            SetAddressFrom(ethereumContractFunctionMessage);
            var queryHandler = EthApiContractService.GetContractQueryHandler<TEthereumContractFunctionMessage>();
            return queryHandler.QueryDeserializingToObjectAsync<TEthereumFunctionReturn>(ethereumContractFunctionMessage,
                ContractAddress, blockParameter);
        }

        public Task<TReturn> QueryAsync<TEthereumContractFunctionMessage, TReturn>(TEthereumContractFunctionMessage ethereumContractFunctionMessage, BlockParameter blockParameter = null)
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
            SetAddressFrom(ethereumContractFunctionMessage);
            var queryHandler = EthApiContractService.GetContractQueryHandler<TEthereumContractFunctionMessage>();
            return queryHandler.QueryAsync<TReturn>(ethereumContractFunctionMessage,
                ContractAddress, blockParameter);
        }

        protected void SetAddressFrom(ContractMessage contractMessage)
        {
            contractMessage.FromAddress = contractMessage.FromAddress ?? AddressFrom;
        }
    }
#endif
}