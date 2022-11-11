using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.ContractHandlers
{
    public interface IContractTransactionHandler<TContractMessage> where TContractMessage : FunctionMessage, new()
    {
        Task<TransactionInput> CreateTransactionInputEstimatingGasAsync(string contractAddress, TContractMessage functionMessage = null);
        Task<HexBigInteger> EstimateGasAsync(string contractAddress, TContractMessage functionMessage = null);     
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractAddress, TContractMessage functionMessage = null, CancellationTokenSource tokenSource = null);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractAddress, TContractMessage functionMessage, CancellationToken cancellationToken);
        Task<string> SendRequestAsync(string contractAddress, TContractMessage functionMessage = null);
        Task<string> SignTransactionAsync(string contractAddress, TContractMessage functionMessage = null);
    }
}