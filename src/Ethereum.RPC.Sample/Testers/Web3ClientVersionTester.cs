using edjCase.JsonRpc.Client;
using System;
using Ethereum.RPC.Web3;

namespace Ethereum.RPC.Sample.Testers
{
    public class Web3ClientVersionTester : IRPCRequestTester
    {
        public dynamic ExecuteTest(RpcClient client)
        {
            var web3ClientVersion = new Web3ClientVersion();
            return web3ClientVersion.SendRequestAsync(client).Result;
        }

        public Type GetRequestType()
        {
            return typeof(Web3ClientVersion);
        }
    }
}
