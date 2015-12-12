
using edjCase.JsonRpc.Client;
using System;
using Ethereum.RPC.Eth;

namespace Ethereum.RPC.Sample.Testers
{
    public class EthNewPendingTransactionFilterTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var ethNewPendingTransactionFilter = new EthNewPendingTransactionFilter();
            return ethNewPendingTransactionFilter.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(EthNewPendingTransactionFilter);
        }
    }
}
        