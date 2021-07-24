using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Admin;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.Tests.Testers
{
    public class AdminPeersTester : RPCRequestTester<JArray>, IRPCRequestTester
    {
        public override async Task<JArray> ExecuteAsync(IClient client)
        {
            var adminPeers = new AdminPeers(client);
            return await adminPeers.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(AdminPeers);
        }

        [Fact]
        public async void ShouldReturnEmptyArray()
        {
            var result = await ExecuteAsync();
            Assert.True(result.Count == 0);
        }
    }
}