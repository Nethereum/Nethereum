using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.TransactionManagers
{
#if !DOTNET35
    public class EtherTransferService : IEtherTransferService
    {
        private readonly ITransactionManager _transactionManager;

        public EtherTransferService(ITransactionManager transactionManager)
        {
            _transactionManager = transactionManager;
        }

        public Task<string> TransferEtherAsync(string toAddress, decimal etherAmount, decimal? gasPriceGwei = null, BigInteger? gas = null)
        {
            var fromAddress = _transactionManager?.Account?.Address;
            var transactionInput = EtherTransferTransactionInputBuilder.CreateTransactionInput(fromAddress, toAddress, etherAmount, gasPriceGwei, gas);
            return _transactionManager.SendTransactionAsync(transactionInput);
        }

        public Task<TransactionReceipt> TransferEtherAndWaitForReceiptAsync(string toAddress, decimal etherAmount, decimal? gasPriceGwei = null, BigInteger? gas = null, CancellationTokenSource tokenSource = null)
        {
            var fromAddress = _transactionManager?.Account?.Address;
            var transactionInput = EtherTransferTransactionInputBuilder.CreateTransactionInput(fromAddress, toAddress, etherAmount, gasPriceGwei, gas);
            return _transactionManager.SendTransactionAndWaitForReceiptAsync(transactionInput, tokenSource);
        }

    }
#endif
}