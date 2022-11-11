

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

    public class IbftGetValidatorsByBlockHashTester : RPCRequestTester<string[]>, IRPCRequestTester
    {
        public override Task<string[]> ExecuteAsync(IClient client)
        {
            var ibftGetValidatorsByBlockHash = new IbftGetValidatorsByBlockHash(client);
            return ibftGetValidatorsByBlockHash.SendRequestAsync(Settings.GetBlockHash());
        }

        public override Type GetRequestType()
        {
            return typeof(IbftGetValidatorsByBlockHash);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }

}
        