using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh.KeyPair;
using Nethereum.RPC.Shh.SymKey;
using Nethereum.RPC.Tests.Testers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class ShhSymKeyTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnResult()
        {
            var addSymKey = new ShhAddSymKey(Client);
            var hasSymKey = new ShhHasSymKey(Client);
            var getSymKey = new ShhGetSymKey(Client); 
            var deleteSymKey = new ShhDeleteSymKey(Client);

            var addResult = await addSymKey.SendRequestAsync(Settings.GetDefaultShhPrivateKey());
            var hasSymKeyResult = await hasSymKey.SendRequestAsync(addResult);
            var getSymKeyResult = await getSymKey.SendRequestAsync(addResult); 
            var deleteSymKeyResult = await deleteSymKey.SendRequestAsync(addResult);

            Assert.NotNull(addResult);
            Assert.True(hasSymKeyResult);
            Assert.Equal(Settings.GetDefaultShhPrivateKey(), getSymKeyResult); 
            Assert.True(deleteSymKeyResult);
        }

        public override Task<bool> ExecuteAsync(IClient client)
        {
            var shhAddPrivateKey = new ShhDeleteKeyPair(client);
            return shhAddPrivateKey.SendRequestAsync(Settings.GetDefaultShhPrivateKey());
        }

        public override Type GetRequestType()
        {
            return typeof(ShhAddPrivateKey);
        }
    }
}
