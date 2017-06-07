using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using System.Numerics;

namespace Nethereum.RPC.TransactionManagers
{
    public abstract class TransactionManagerBase : ITransactionManager
    {
        public virtual IClient Client { get; set; }
        public abstract BigInteger DefaultGasPrice { get; set; }
        public abstract BigInteger DefaultGas { get; set; }

        public virtual Task<HexBigInteger> EstimateGasAsync<T>(T callInput) where T : CallInput
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (callInput == null) throw new ArgumentNullException(nameof(callInput));
            var ethEstimateGas = new EthEstimateGas(Client);
            return ethEstimateGas.SendRequestAsync(callInput);
        }

        public abstract Task<string> SendTransactionAsync<T>(T transactionInput) where T : TransactionInput;
        
        public virtual Task<string> SendTransactionAsync(string from, string to, HexBigInteger amount)
        {  
            return SendTransactionAsync(new TransactionInput() { From = from, To = to, Value = amount});
        }

        protected void SetDefaultGasPriceAndCostIfNotSet(TransactionInput transactionInput)
        {
            if (DefaultGasPrice != null)
            {
                if (transactionInput.GasPrice == null) transactionInput.GasPrice = new HexBigInteger(DefaultGasPrice);
            }

            if (DefaultGas != null)
            {
                if (transactionInput.Gas == null) transactionInput.Gas = new HexBigInteger(DefaultGas);
            }
        }
    }
}