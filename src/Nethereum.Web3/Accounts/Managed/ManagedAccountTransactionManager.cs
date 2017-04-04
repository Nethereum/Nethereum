using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Personal;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Web3.Accounts.Managed
{
    public class ManagedAccountTransactionManager : TransactionManagerBase
    {
        private readonly string _accountAddress;
        private readonly string _password;

        public ManagedAccountTransactionManager(IClient client, string accountAddress, string password)
        {
            _accountAddress = accountAddress;
            _password = password;
            Client = client;
        }

        public ManagedAccountTransactionManager(string accountAddress, string password):this(null, accountAddress, password)
        {
 
        }

        public override Task<string> SendTransactionAsync<T>(T transactionInput)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            if (transactionInput.From != _accountAddress) throw new Exception("Invalid account used");
            var ethSendTransaction = new PersonalSignAndSendTransaction(Client);
            return ethSendTransaction.SendRequestAsync(transactionInput, _password);
        }

        public override Task<string> SendTransactionAsync(string from, string to, HexBigInteger amount)
        {
            if (from != _accountAddress) throw new Exception("Invalid account used");
            return base.SendTransactionAsync(from, to, amount);
        }
    }
}