using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Network;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.Network
{
    public class ParityGasPriceHistogramTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var parityGasPriceHistogram = new ParityGasPriceHistogram(client);
            return await parityGasPriceHistogram.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityGasPriceHistogram);
        }

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);

            var bucketBounds = result["bucketBounds"] as JArray;
            Assert.NotNull(bucketBounds);
            Assert.NotEmpty(bucketBounds);

        }
    }
}