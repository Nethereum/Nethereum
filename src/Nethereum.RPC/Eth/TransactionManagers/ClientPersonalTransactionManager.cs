using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Personal;

namespace Nethereum.RPC.Eth.TransactionManagers
{
    public class ClientPersonalTransactionManager : ITransactionManager
    {
        private readonly string _password;
        private readonly PersonalSignAndSendTransaction _ethSendTransaction;

        public ClientPersonalTransactionManager(IClient rpcClient, string password)
        {
            _password = password;
            _ethSendTransaction = new PersonalSignAndSendTransaction(rpcClient);
        }

        public Task<string> SendTransactionAsync<T>(T transactionInput) where T : TransactionInput
        {
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            return _ethSendTransaction.SendRequestAsync(transactionInput, _password);
        }
    }
}