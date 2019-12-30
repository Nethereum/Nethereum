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
    public class ShhKeyPairTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnResult()
        {
            var addPrivateKey = new ShhAddPrivateKey(Client);
            var hasKeyPair = new ShhHasKeyPair(Client);
            var getPrivateKey = new ShhGetPrivateKey(Client);
            var getPublicKey = new ShhGetPublicKey(Client);
            var deleteKeyPair = new ShhDeleteKeyPair(Client);

            var addResult = await addPrivateKey.SendRequestAsync(Settings.GetDefaultShhPrivateKey());
            var hasKeyPairResult = await hasKeyPair.SendRequestAsync(addResult);
            var getPrivateKeyResult = await getPrivateKey.SendRequestAsync(addResult);
            var getPublicKeyResult = await getPublicKey.SendRequestAsync(addResult); 
            var deleteKeyPairResult = await deleteKeyPair.SendRequestAsync(addResult);

            Assert.NotNull(addResult);
            Assert.True(hasKeyPairResult);
            Assert.Equal(Settings.GetDefaultShhPrivateKey(), getPrivateKeyResult);
            Assert.Equal(Settings.GetDefaultShhPublicKey(), getPublicKeyResult);
            Assert.True(deleteKeyPairResult);
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
