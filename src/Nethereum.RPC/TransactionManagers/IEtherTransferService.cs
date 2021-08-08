using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;

namespace Nethereum.RPC.TransactionManagers
{
    public interface IEtherTransferService
    {
        Task<TransactionReceipt> TransferEtherAndWaitForReceiptAsync(string toAddress, decimal etherAmount, decimal? gasPriceGwei = null, BigInteger? gas = null, BigInteger? nonce = null, CancellationTokenSource tokenSource = null);
        Task<string> TransferEtherAsync(string toAddress, decimal etherAmount, decimal? gasPriceGwei = null, BigInteger? gas = null, BigInteger? nonce = null);
        Task<decimal> CalculateTotalAmountToTransferWholeBalanceInEtherAsync(string address, decimal gasPriceGwei, BigInteger? gas = null);
        Task<decimal> CalculateTotalAmountToTransferWholeBalanceInEtherAsync(string address,
            BigInteger maxFeePerGas, BigInteger? gas = null);
        Task<string> TransferEtherAsync(string toAddress, decimal etherAmount, BigInteger maxPriorityFee,
            BigInteger maxFeePerGas, BigInteger? gas = null,
            BigInteger? nonce = null);

        Task<TransactionReceipt> TransferEtherAndWaitForReceiptAsync(string toAddress, decimal etherAmount, BigInteger maxPriorityFee,
            BigInteger maxFeePerGas, BigInteger? gas = null, BigInteger? nonce = null,
            CancellationTokenSource tokenSource = null);
        Task<BigInteger> EstimateGasAsync(string toAddress, decimal etherAmount);

        Task<Fee1559> SuggestFeeToTransferWholeBalanceInEtherAsync(
            BigInteger? maxPriorityFeePerGas = null);
    }
}