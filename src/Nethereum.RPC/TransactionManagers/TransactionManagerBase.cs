using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using System.Numerics;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.TransactionReceipts;

namespace Nethereum.RPC.TransactionManagers
{
    public abstract class TransactionManagerBase : ITransactionManager
    {
        public virtual IClient Client { get; set; }
        public abstract BigInteger DefaultGasPrice { get; set; }
        public abstract BigInteger DefaultGas { get; set; }
        public IAccount Account { get; protected set; }
        public abstract Task<string> SignTransactionAsync(TransactionInput transaction);
        public abstract Task<string> SignTransactionRetrievingNextNonceAsync(TransactionInput transaction);

#if !DOTNET35
        private ITransactionReceiptService _transactionReceiptService;
        public ITransactionReceiptService TransactionReceiptService {
            get
            {
                if (_transactionReceiptService == null) return TransactionReceiptServiceFactory.GetDefaultransactionReceiptService(this);
                return _transactionReceiptService;
            }
            set
            {
                _transactionReceiptService = value;
            }
        }
#endif               
        public virtual Task<HexBigInteger> EstimateGasAsync(CallInput callInput)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (callInput == null) throw new ArgumentNullException(nameof(callInput));
            var ethEstimateGas = new EthEstimateGas(Client);
            return ethEstimateGas.SendRequestAsync(callInput);
        }

        public abstract Task<string> SendTransactionAsync(TransactionInput transactionInput);
        
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