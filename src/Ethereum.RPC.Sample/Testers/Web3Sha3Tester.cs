using System;
using edjCase.JsonRpc.Client;
using Ethereum.RPC.Web3;

namespace Ethereum.RPC.Sample
{
    public class Web3Sha3Tester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var web3Sha3 = new Web3Sha3();
            return web3Sha3.SendRequestAsync(client, "Monkey").Result;
        }

        public Type GetRequestType()
        {
            return typeof(Web3Sha3Tester);
        }
    }
}