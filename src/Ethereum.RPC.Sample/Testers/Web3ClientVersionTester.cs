using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;
using Ethereum.RPC.Web3;

namespace Ethereum.RPC.Sample.Testers
{
    public class Web3ClientVersionTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var web3ClientVersion = new Web3ClientVersion();
            return await web3ClientVersion.SendRequestAsync(client);
        }

        public Type GetRequestType()
        {
            return typeof(Web3ClientVersion);
        }
    }
}
