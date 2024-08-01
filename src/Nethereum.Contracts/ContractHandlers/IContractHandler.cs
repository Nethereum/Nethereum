using Nethereum.ABI.Decoders;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Contracts.ContractHandlers
{
    public interface IContractHandler
    {
        string ContractAddress { get; }
        EthApiContractService EthApiContractService { get; }

#if !DOTNET35
        Task<HexBigInteger> EstimateGasAsync<TEthereumContractFunctionMessage>(TEthereumContractFunctionMessage transactionMessage = null) where TEthereumContractFunctionMessage : FunctionMessage, new();
        Event<TEventType> GetEvent<TEventType>() where TEventType : IEventDTO, new();
        Function<TEthereumContractFunctionMessage> GetFunction<TEthereumContractFunctionMessage>() where TEthereumContractFunctionMessage : new();
        Task<TReturn> QueryAsync<TEthereumContractFunctionMessage, TReturn>(TEthereumContractFunctionMessage ethereumContractFunctionMessage = null, BlockParameter blockParameter = null) where TEthereumContractFunctionMessage : FunctionMessage, new();
        Task<TEthereumFunctionReturn> QueryDeserializingToObjectAsync<TEthereumContractFunctionMessage, TEthereumFunctionReturn>(TEthereumContractFunctionMessage ethereumContractFunctionMessage = null, BlockParameter blockParameter = null)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
            where TEthereumFunctionReturn : IFunctionOutputDTO, new();
        Task<TReturn> QueryRawAsync<TEthereumContractFunctionMessage, TCustomDecoder, TReturn>(BlockParameter blockParameter = null)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
            where TCustomDecoder : ICustomRawDecoder<TReturn>, new();
        Task<TReturn> QueryRawAsync<TEthereumContractFunctionMessage, TCustomDecoder, TReturn>(TEthereumContractFunctionMessage ethereumContractFunctionMessage, BlockParameter blockParameter = null)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
            where TCustomDecoder : ICustomRawDecoder<TReturn>, new();
        Task<byte[]> QueryRawAsync<TEthereumContractFunctionMessage>(TEthereumContractFunctionMessage ethereumContractFunctionMessage = null, BlockParameter blockParameter = null) where TEthereumContractFunctionMessage : FunctionMessage, new();
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(TEthereumContractFunctionMessage transactionMessage = null, CancellationTokenSource tokenSource = null) where TEthereumContractFunctionMessage : FunctionMessage, new();
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(TEthereumContractFunctionMessage transactionMessage, CancellationToken cancellationToken) where TEthereumContractFunctionMessage : FunctionMessage, new();
        Task<string> SendRequestAsync<TEthereumContractFunctionMessage>(TEthereumContractFunctionMessage transactionMessage = null) where TEthereumContractFunctionMessage : FunctionMessage, new();
        Task<string> SignTransactionAsync<TEthereumContractFunctionMessage>(TEthereumContractFunctionMessage transactionMessage = null) where TEthereumContractFunctionMessage : FunctionMessage, new();
#endif
    }
}