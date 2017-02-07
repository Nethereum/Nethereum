using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Personal;

namespace Nethereum.RPC.Eth.TransactionManagers
{
    public class ClientPersonalTransactionManager : ITransactionManager
    {
        private readonly string _accountAddress;
        private readonly string _password;

        public ClientPersonalTransactionManager(IClient client, string accountAddress, string password)
        {
            _accountAddress = accountAddress;
            _password = password;
            Client = client;
        }

        public ClientPersonalTransactionManager(string accountAddress, string password):this(null, accountAddress, password)
        {
 
        }

        public IClient Client { get; set; }

        public Task<string> SendTransactionAsync<T>(T transactionInput) where T : TransactionInput
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transactionInput.From != _accountAddress) throw new Exception("Invalid account used signing");
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            var ethSendTransaction = new PersonalSignAndSendTransaction(Client);
            return ethSendTransaction.SendRequestAsync(transactionInput, _password);
        }
    }
}