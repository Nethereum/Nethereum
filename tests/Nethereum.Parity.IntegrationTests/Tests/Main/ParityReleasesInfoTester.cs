using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Admin;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.Main
{
    public class ParityReleasesInfoTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var parityReleasesInfo = new ParityReleasesInfo(client);
            return await parityReleasesInfo.SendRequestAsync().ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(ParityReleasesInfo);
        }

        [Fact]
        public async void ShouldSucceed()
        {
            //6th Feb 2019 - given the example curl attempt
            //curl --data '{"method":"parity_releasesInfo","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST localhost:8545
            //parity returns a null result
            //{"jsonrpc":"2.0","result":null,"id":1}
            //so we can no longer check for not null
            await ExecuteAsync().ConfigureAwait(false);
        }
    }
}