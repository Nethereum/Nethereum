using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Web3;

namespace Nethereum.RPC.Tests.Testers
{
    public class Web3ClientVersionTester : IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            var web3ClientVersion = new Web3ClientVersion(client);
            return await web3ClientVersion.SendRequestAsync().ConfigureAwait(false);
        }

        public Type GetRequestType()
        {
            return typeof (Web3ClientVersion);
        }
    }
}