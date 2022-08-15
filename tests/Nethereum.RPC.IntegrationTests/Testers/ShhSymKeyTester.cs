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

            var addResult = await addSymKey.SendRequestAsync(Settings.GetDefaultShhPrivateKey()).ConfigureAwait(false);
            var hasSymKeyResult = await hasSymKey.SendRequestAsync(addResult).ConfigureAwait(false);
            var getSymKeyResult = await getSymKey.SendRequestAsync(addResult).ConfigureAwait(false); 
            var deleteSymKeyResult = await deleteSymKey.SendRequestAsync(addResult).ConfigureAwait(false);

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
