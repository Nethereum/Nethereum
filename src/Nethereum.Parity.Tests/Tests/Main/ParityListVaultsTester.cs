
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Admin;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityListVaultsTester : RPCRequestTester<string[]>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string[]> ExecuteAsync(IClient client)
        {
            var parityListVaults = new ParityListVaults(client);
            return await parityListVaults.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityListVaults);
        }
    }
}
        