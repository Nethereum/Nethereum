

using System;
using System.Threading.Tasks;
using Nethereum.Besu.RPC.IBFT;
using Nethereum.JsonRpc.Client;
using Nethereum.Besu.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Besu.Tests.Testers
{

    public class IbftProposeValidatorVoteTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        public override Task<bool> ExecuteAsync(IClient client)
        {
            var ibftProposeValidatorVote = new IbftProposeValidatorVote(client);
            return ibftProposeValidatorVote.SendRequestAsync(Settings.GetDefaultAccount() , true);
        }

        public override Type GetRequestType()
        {
            return typeof(IbftProposeValidatorVote);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }

}
        