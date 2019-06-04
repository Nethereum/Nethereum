

using System;
using System.Threading.Tasks;
using Nethereum.Pantheon.RPC.IBFT;
using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Pantheon.Tests.Testers
{

    public class IbftGetPendingVotesTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var ibftGetPendingVotes = new IbftGetPendingVotes(client);
            return await ibftGetPendingVotes.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(IbftGetPendingVotes);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        