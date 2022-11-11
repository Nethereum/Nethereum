

using System;
using System.Threading.Tasks;
using Nethereum.Besu.RPC.IBFT;
using Nethereum.JsonRpc.Client;
using Nethereum.Besu.IntegrationTests;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Besu.Tests.Testers
{

    public class IbftGetValidatorsByBlockNumberTester : RPCRequestTester<string[]>, IRPCRequestTester
    {
        public override Task<string[]> ExecuteAsync(IClient client)
        {
            var ibftGetValidatorsByBlockNumber = new IbftGetValidatorsByBlockNumber(client);
            return ibftGetValidatorsByBlockNumber.SendRequestAsync(BlockParameter.CreateLatest());
        }

        public override Type GetRequestType()
        {
            return typeof(IbftGetValidatorsByBlockNumber);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }

}
        