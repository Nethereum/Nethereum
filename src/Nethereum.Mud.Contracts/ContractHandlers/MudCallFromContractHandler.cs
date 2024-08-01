using System.Threading;
using System.Threading.Tasks;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts;
using Nethereum.Mud.Contracts.World.ContractDefinition;
using Nethereum.Web3;
using Nethereum.Mud.Contracts.Core.Systems;
namespace Nethereum.Mud.Contracts.ContractHandlers
{
    public class MudCallFromContractHandler : ContractHandler
    {

        public static MudCallFromContractHandler Create(string contractAddress, EthApiContractService ethApiContractService, string delegator, IResource resource, string addressFrom = null)
        {
            return new MudCallFromContractHandler(contractAddress, ethApiContractService, delegator, resource, addressFrom);
        }

        public static MudCallFromContractHandler CreateFromExistingContractService(ISystemService service, string delegatorAddress)
        {
            var contractAddress = service.ContractAddress;
            var ethApiContractService = service.ContractHandler.EthApiContractService;
            var handler = new MudCallFromContractHandler(contractAddress, ethApiContractService, delegatorAddress, service.Resource, service.ContractHandler.AddressFrom);
            return handler;
        }

        public MudCallFromContractHandler(string contractAddress, EthApiContractService ethApiContractService, string delegator, IResource resource,
            string addressFrom = null):base(contractAddress, ethApiContractService, addressFrom)
        {
            Delegator = delegator;
            Resource = resource;
        }
        public string Delegator { get; protected set; }
        public IResource Resource { get; protected set; }


#if !DOTNET35

        public override Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null, CancellationTokenSource tokenSource = null)
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var command = EthApiContractService.GetContractTransactionHandler<CallFromFunction>();
           
            return command.SendRequestAndWaitForReceiptAsync(ContractAddress, CreateCallFromFunction(transactionMessage), tokenSource);
        }

        private CallFromFunction CreateCallFromFunction<TEthereumContractFunctionMessage>(TEthereumContractFunctionMessage functionMessage) where TEthereumContractFunctionMessage : FunctionMessage, new ()
        {
            var callFromFunction = new CallFromFunction();
            if(Delegator != null)
            {
                callFromFunction.Delegator = Delegator;
            }
            else
            {
                callFromFunction.Delegator = functionMessage.FromAddress ?? AddressFrom;
            }

            callFromFunction.SystemId = Resource.ResourceIdEncoded;
            callFromFunction.CallData = functionMessage.GetCallData();
            SetAddressFrom(callFromFunction);
            return callFromFunction;
        }

        public override Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage, CancellationToken cancellationToken)
    
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var command = EthApiContractService.GetContractTransactionHandler<CallFromFunction>();
            return command.SendRequestAndWaitForReceiptAsync(ContractAddress, CreateCallFromFunction(transactionMessage), cancellationToken);
        }

        public override Task<string> SendRequestAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var command = EthApiContractService.GetContractTransactionHandler<CallFromFunction>();
            return command.SendRequestAsync(ContractAddress, CreateCallFromFunction(transactionMessage));
        }

        public override Task<string> SignTransactionAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
           
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var command = EthApiContractService.GetContractTransactionHandler<CallFromFunction>();
            return command.SignTransactionAsync(ContractAddress, CreateCallFromFunction(transactionMessage));
        }

        public override Task<HexBigInteger> EstimateGasAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var command = EthApiContractService.GetContractTransactionHandler<CallFromFunction>();
            return command.EstimateGasAsync(ContractAddress, CreateCallFromFunction(transactionMessage));
        }

        public override Task<TEthereumFunctionReturn> QueryDeserializingToObjectAsync<TEthereumContractFunctionMessage,
            TEthereumFunctionReturn>(TEthereumContractFunctionMessage ethereumContractFunctionMessage = null,
            BlockParameter blockParameter = null)
           
        {
            if (ethereumContractFunctionMessage == null) ethereumContractFunctionMessage = new TEthereumContractFunctionMessage();
            SetAddressFrom(ethereumContractFunctionMessage);
            var queryHandler = EthApiContractService.GetContractQueryHandler<CallFromFunction>();
            return queryHandler.QueryDeserializingToObjectAsync<TEthereumFunctionReturn>(
                CreateCallFromFunction(ethereumContractFunctionMessage),
                ContractAddress, blockParameter);
        }

        public override Task<TReturn> QueryAsync<TEthereumContractFunctionMessage, TReturn>(
            TEthereumContractFunctionMessage ethereumContractFunctionMessage = null, BlockParameter blockParameter = null)
          
        {
            if (ethereumContractFunctionMessage == null) ethereumContractFunctionMessage = new TEthereumContractFunctionMessage();
            SetAddressFrom(ethereumContractFunctionMessage);
            var queryHandler = EthApiContractService.GetContractQueryHandler<CallFromFunction>();
            return queryHandler.QueryAsync<TReturn>(
                ContractAddress, CreateCallFromFunction(ethereumContractFunctionMessage), blockParameter);
        }

        public override Task<byte[]> QueryRawAsync<TEthereumContractFunctionMessage>(TEthereumContractFunctionMessage ethereumContractFunctionMessage = null, BlockParameter blockParameter = null)
          
        {
            if (ethereumContractFunctionMessage == null) ethereumContractFunctionMessage = new TEthereumContractFunctionMessage();
            SetAddressFrom(ethereumContractFunctionMessage);
            var queryHandler = EthApiContractService.GetContractQueryHandler<CallFromFunction>();
            return queryHandler.QueryRawAsBytesAsync(
                ContractAddress, CreateCallFromFunction(ethereumContractFunctionMessage), blockParameter);
        }
#endif

    }

}