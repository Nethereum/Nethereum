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

    public class ShhPostTester : RPCRequestTester<string>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnResult()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var shhNewKeyPair = new ShhPost(client);
            var shhAddPrivateKey = new ShhAddPrivateKey(client);

            var keyPair = await shhAddPrivateKey.SendRequestAsync(Settings.GetDefaultPrivateKey());
            //4 Bytes
            var topic = UTF8Encoding.ASCII.GetBytes("default_topic").Take(4).ToArray().ToHex(true);
            var payload = UTF8Encoding.ASCII.GetBytes("default_message").ToHex(true);
            return await shhNewKeyPair.SendRequestAsync(new Shh.DTOs.MessageInput
            {
                PubKey = Settings.GetDefaultPublicKey(),
                Ttl = 7,
                Topic = topic,
                PowTarget = 2.1,
                PowTime = 100,
                Payload = payload,
                Sig = keyPair
            });
        }

        public override Type GetRequestType()
        {
            return typeof(ShhNewKeyPair);
        }
    }
}