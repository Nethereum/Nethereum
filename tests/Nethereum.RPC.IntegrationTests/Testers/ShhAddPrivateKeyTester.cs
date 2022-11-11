using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh.KeyPair;
using Nethereum.RPC.Tests.Testers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class ShhAddPrivateKeyTester : RPCRequestTester<string>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnTheKeyPairID()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }

        public override Task<string> ExecuteAsync(IClient client)
        {
            var shhAddPrivateKey = new ShhAddPrivateKey(client);
            return shhAddPrivateKey.SendRequestAsync(Settings.GetDefaultShhPrivateKey());
        }

        public override Type GetRequestType()
        {
            return typeof(ShhAddPrivateKey);
        }
    }
}
