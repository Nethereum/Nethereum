using System.Threading;
using System.Threading.Tasks;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.ContractHandlers
{
    public class ContractHandler : IContractHandler
    {
        public ContractHandler(string contractAddress, IEthApiContractService ethApiContractService,
            string addressFrom = null)
        {
            ContractAddress = contractAddress;
            EthApiContractService = ethApiContractService;
            AddressFrom = addressFrom;
        }

        public string AddressFrom { get; protected set; }

        public string ContractAddress { get; }
        public IEthApiContractService EthApiContractService { get; protected set; }

        public Event<TEventType> GetEvent<TEventType>() where TEventType : IEventDTO, new()
        {
            if (!EventAttribute.IsEventType(typeof(TEventType))) return null;
            return new Event<TEventType>(EthApiContractService.Client, ContractAddress);
        }

        public virtual Function<TEthereumContractFunctionMessage> GetFunction<TEthereumContractFunctionMessage>() where TEthereumContractFunctionMessage : new()
        {
            var contract = EthApiContractService.GetContract<TEthereumContractFunctionMessage>(ContractAddress);
            return contract.GetFunction<TEthereumContractFunctionMessage>();
        }

        protected void SetAddressFrom(ContractMessageBase contractMessage)
        {
            contractMessage.FromAddress = contractMessage.FromAddress ?? AddressFrom;
        }

#if !DOTNET35

        public virtual Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null, CancellationTokenSource tokenSource = null)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var command = EthApiContractService.GetContractTransactionHandler<TEthereumContractFunctionMessage>();
            SetAddressFrom(transactionMessage);
            return command.SendRequestAndWaitForReceiptAsync(ContractAddress, transactionMessage, tokenSource);
        }

        public virtual Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage, CancellationToken cancellationToken)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var command = EthApiContractService.GetContractTransactionHandler<TEthereumContractFunctionMessage>();
            SetAddressFrom(transactionMessage);
            return command.SendRequestAndWaitForReceiptAsync(ContractAddress, transactionMessage, cancellationToken);
        }

        public virtual Task<string> SendRequestAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var command = EthApiContractService.GetContractTransactionHandler<TEthereumContractFunctionMessage>();
            SetAddressFrom(transactionMessage);
            return command.SendRequestAsync(ContractAddress, transactionMessage);
        }

        public virtual Task<string> SignTransactionAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var command = EthApiContractService.GetContractTransactionHandler<TEthereumContractFunctionMessage>();
            SetAddressFrom(transactionMessage);
            return command.SignTransactionAsync(ContractAddress, transactionMessage);
        }

        public virtual Task<HexBigInteger> EstimateGasAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var command = EthApiContractService.GetContractTransactionHandler<TEthereumContractFunctionMessage>();
            SetAddressFrom(transactionMessage);
            return command.EstimateGasAsync(ContractAddress, transactionMessage);
        }

        public virtual Task<TEthereumFunctionReturn> QueryDeserializingToObjectAsync<TEthereumContractFunctionMessage,
            TEthereumFunctionReturn>(TEthereumContractFunctionMessage ethereumContractFunctionMessage = null,
            BlockParameter blockParameter = null)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
            where TEthereumFunctionReturn : IFunctionOutputDTO, new()
        {
            if (ethereumContractFunctionMessage == null) ethereumContractFunctionMessage = new TEthereumContractFunctionMessage();
            SetAddressFrom(ethereumContractFunctionMessage);
            var queryHandler = EthApiContractService.GetContractQueryHandler<TEthereumContractFunctionMessage>();
            return queryHandler.QueryDeserializingToObjectAsync<TEthereumFunctionReturn>(
                ethereumContractFunctionMessage,
                ContractAddress, blockParameter);
        }

        public virtual Task<TReturn> QueryAsync<TEthereumContractFunctionMessage, TReturn>(
            TEthereumContractFunctionMessage ethereumContractFunctionMessage = null, BlockParameter blockParameter = null)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            if (ethereumContractFunctionMessage == null) ethereumContractFunctionMessage = new TEthereumContractFunctionMessage();
            SetAddressFrom(ethereumContractFunctionMessage);
            var queryHandler = EthApiContractService.GetContractQueryHandler<TEthereumContractFunctionMessage>();
            return queryHandler.QueryAsync<TReturn>(
                ContractAddress, ethereumContractFunctionMessage, blockParameter);
        }

        public virtual Task<byte[]> QueryRawAsync<TEthereumContractFunctionMessage>(TEthereumContractFunctionMessage ethereumContractFunctionMessage = null, BlockParameter blockParameter = null)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            if (ethereumContractFunctionMessage == null) ethereumContractFunctionMessage = new TEthereumContractFunctionMessage();
            SetAddressFrom(ethereumContractFunctionMessage);
            var queryHandler = EthApiContractService.GetContractQueryHandler<TEthereumContractFunctionMessage>();
            return queryHandler.QueryRawAsBytesAsync(
                ContractAddress, ethereumContractFunctionMessage, blockParameter);
        }

        public virtual Task<TReturn> QueryRawAsync<TEthereumContractFunctionMessage, TCustomDecoder, TReturn>(BlockParameter blockParameter = null)
           where TEthereumContractFunctionMessage : FunctionMessage, new()
           where TCustomDecoder : ICustomRawDecoder<TReturn>, new()
        {
            var ethereumContractFunctionMessage = new TEthereumContractFunctionMessage();
            return QueryRawAsync<TEthereumContractFunctionMessage, TCustomDecoder, TReturn>(ethereumContractFunctionMessage, blockParameter);
        }

        public virtual async Task<TReturn> QueryRawAsync<TEthereumContractFunctionMessage, TCustomDecoder, TReturn>(TEthereumContractFunctionMessage ethereumContractFunctionMessage, BlockParameter blockParameter = null)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
            where TCustomDecoder : ICustomRawDecoder<TReturn>, new()
        {

            var result = await QueryRawAsync<TEthereumContractFunctionMessage>(ethereumContractFunctionMessage,
                blockParameter).ConfigureAwait(false);
            var decoder = new TCustomDecoder();
            return decoder.Decode(result);
        }
#endif

    }

}