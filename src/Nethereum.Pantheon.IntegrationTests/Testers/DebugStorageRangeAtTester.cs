

using System;
using System.Threading.Tasks;
using Nethereum.Pantheon.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Pantheon.Tests.Testers
{

    public class DebugStorageRangeAtTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var debugStorageRangeAt = new DebugStorageRangeAt(client);
            string blockHash = Settings.GetBlockHash();
            string contractAddress = Settings.GetContractAddress();
            return await debugStorageRangeAt.SendRequestAsync(blockHash, 0, contractAddress, "0x0000000000000000000000000000000000000000000000000000000000000000", 1);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugStorageRangeAt);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        