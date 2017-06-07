using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Transactions;
using System.Numerics;

namespace Nethereum.RPC.TransactionManagers
{
    public class TransactionManager : TransactionManagerBase
    {
        public override BigInteger DefaultGasPrice { get; set; }
        public override BigInteger DefaultGas { get; set; }

        public TransactionManager(IClient client)
        {
            this.Client = client;
        }

        public override Task<string> SendTransactionAsync<T>(T transactionInput)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            SetDefaultGasPriceAndCostIfNotSet(transactionInput);
            return new EthSendTransaction(Client).SendRequestAsync(transactionInput);
        }
    }
}