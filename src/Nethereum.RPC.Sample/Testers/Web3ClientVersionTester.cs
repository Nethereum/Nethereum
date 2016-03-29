using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Web3;

namespace Nethereum.RPC.Sample.Testers
{
    public class Web3ClientVersionTester : IRPCRequestTester
    {
        public async Task<dynamic> ExecuteTestAsync(RpcClient client)
        {
            var web3ClientVersion = new Web3ClientVersion(client);
            return await web3ClientVersion.SendRequestAsync();
        }

        public Type GetRequestType()
        {
            return typeof (Web3ClientVersion);
        }
    }
}