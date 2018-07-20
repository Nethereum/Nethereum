using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

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
            var transactionInput = CreateTransactionInput(toAddress, etherAmount, gasPriceGwei, gas);
            return _transactionManager.SendTransactionAsync(transactionInput);
        }

        public Task<TransactionReceipt> TransferEtherAndWaitForReceiptAsync(string toAddress, decimal etherAmount, decimal? gasPriceGwei = null, BigInteger? gas = null, CancellationTokenSource tokenSource = null)
        {
            var transactionInput = CreateTransactionInput(toAddress, etherAmount, gasPriceGwei, gas);
            return _transactionManager.SendTransactionAndWaitForReceiptAsync(transactionInput, tokenSource);
        }

        private TransactionInput CreateTransactionInput(string toAddress, decimal etherAmount, decimal? gasPriceGwei = null, BigInteger? gas = null)
        {
            if (toAddress == null) throw new ArgumentNullException(nameof(toAddress));
            if (etherAmount <= 0) throw new ArgumentOutOfRangeException(nameof(etherAmount));
            if (gasPriceGwei <= 0) throw new ArgumentOutOfRangeException(nameof(gasPriceGwei));

            var transactionInput = new TransactionInput()
            {
                To = toAddress,
                From = _transactionManager?.Account?.Address,
                GasPrice = gasPriceGwei == null ? null : new HexBigInteger(UnitConversion.Convert.ToWei(gasPriceGwei.Value, UnitConversion.EthUnit.Gwei)),
                Value = new HexBigInteger(UnitConversion.Convert.ToWei(etherAmount)),
                Gas = gas == null ? null : new HexBigInteger(gas.Value)
            };
            return transactionInput;
        }
    }
#endif
}