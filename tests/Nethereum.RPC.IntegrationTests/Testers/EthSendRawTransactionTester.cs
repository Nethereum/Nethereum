using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthSendRawTransactionTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var ethSendRawTransaction = new EthSendRawTransaction(client);
            return
                await
                    ethSendRawTransaction.SendRequestAsync(
                        "0xd46e8dd67c5d32be8d46e8dd67c5d32be8058bb8eb970870f072445675058bb8eb970870f072445675").ConfigureAwait(false);
        }

        public Type GetRequestType()
        {
            return typeof (EthSendRawTransaction);
        }
    }
}