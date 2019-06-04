

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

    public class IbftProposeValidatorVoteTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var ibftProposeValidatorVote = new IbftProposeValidatorVote(client);
            return await ibftProposeValidatorVote.SendRequestAsync(Settings.GetDefaultAccount() , true);
        }

        public override Type GetRequestType()
        {
            return typeof(IbftProposeValidatorVote);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        