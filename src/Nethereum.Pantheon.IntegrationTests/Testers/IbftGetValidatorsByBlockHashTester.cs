

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

    public class IbftGetValidatorsByBlockHashTester : RPCRequestTester<string[]>, IRPCRequestTester
    {
        public override async Task<string[]> ExecuteAsync(IClient client)
        {
            var ibftGetValidatorsByBlockHash = new IbftGetValidatorsByBlockHash(client);
            return await ibftGetValidatorsByBlockHash.SendRequestAsync(Settings.GetBlockHash());
        }

        public override Type GetRequestType()
        {
            return typeof(IbftGetValidatorsByBlockHash);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        