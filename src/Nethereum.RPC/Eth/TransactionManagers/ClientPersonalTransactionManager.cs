using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Personal;

namespace Nethereum.RPC.Eth.TransactionManagers
{
    public class ClientPersonalTransactionManager : TransactionManagerBase
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

        public override Task<string> SendTransactionAsync<T>(T transactionInput)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            if (transactionInput.From != _accountAddress) throw new Exception("Invalid account used signing");
            var ethSendTransaction = new PersonalSignAndSendTransaction(Client);
            return ethSendTransaction.SendRequestAsync(transactionInput, _password);
        }
    }
}