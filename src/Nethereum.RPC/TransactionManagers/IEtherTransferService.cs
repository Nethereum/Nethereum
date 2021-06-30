using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.TransactionManagers
{
    public interface IEtherTransferService
    {
        Task<TransactionReceipt> TransferEtherAndWaitForReceiptAsync(string toAddress, decimal etherAmount, decimal? gasPriceGwei = null, BigInteger? gas = null, BigInteger? nonce = null, CancellationTokenSource tokenSource = null);
        Task<string> TransferEtherAsync(string toAddress, decimal etherAmount, decimal? gasPriceGwei = null, BigInteger? gas = null, BigInteger? nonce = null);
        Task<decimal> CalculateTotalAmountToTransferWholeBalanceInEther(string address, decimal gasPriceGwei, BigInteger? gas = null);
        Task<decimal> CalculateTotalAmountToTransferWholeBalanceInEther(string address,
            BigInteger maxFeePerGas, BigInteger? gas = null);
        Task<string> TransferEtherAsync(string toAddress, decimal etherAmount, BigInteger maxFeePerGas, BigInteger? gas = null,
            BigInteger? nonce = null);

        Task<TransactionReceipt> TransferEtherAndWaitForReceiptAsync(string toAddress, decimal etherAmount,
            BigInteger maxFeePerGas, BigInteger? gas = null, BigInteger? nonce = null,
            CancellationTokenSource tokenSource = null);
        Task<BigInteger> EstimateGasAsync(string toAddress, decimal etherAmount);

        Task<BigInteger> CalculateMaxFeePerGasToTransferWholeBalanceInEther(
            BigInteger? maxPriorityFeePerGas = null);
    }
}