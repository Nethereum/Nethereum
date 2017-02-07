using System;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.RPC.Eth.TransactionManagers
{
    public class TransactionManager : ITransactionManager
    {
        private readonly EthSendTransaction _ethSendTransaction;

        public TransactionManager(EthApiService ethApiService)
        {
            _ethSendTransaction = ethApiService.Transactions.SendTransaction;
        }

        public TransactionManager(EthSendTransaction ethSendTransaction)
        {
            _ethSendTransaction = ethSendTransaction;
        }

        public Task<string> SendTransactionAsync<T>(T transactionInput) where T : TransactionInput
        {
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            return _ethSendTransaction.SendRequestAsync(transactionInput);
        }
    }
}