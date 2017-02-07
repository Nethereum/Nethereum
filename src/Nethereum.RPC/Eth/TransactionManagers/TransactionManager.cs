using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.RPC.Eth.TransactionManagers
{
    public class TransactionManager : ITransactionManager
    {
        public TransactionManager(IClient client)
        {
            this.Client = client;
        }

        public IClient Client { get; set; }

        public Task<string> SendTransactionAsync<T>(T transactionInput) where T : TransactionInput
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            return new EthSendTransaction(Client).SendRequestAsync(transactionInput);
        }
    }
}