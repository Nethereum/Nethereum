using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh;
using Nethereum.RPC.Shh.KeyPair;
using Nethereum.RPC.Shh.SymKey;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class ShhNewSymKeyTester : RPCRequestTester<string>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnTheSymKey()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var shhNewKeyPair = new ShhNewSymKey(client);
            return await shhNewKeyPair.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ShhNewKeyPair);
        }
    }
}