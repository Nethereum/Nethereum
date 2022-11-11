using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh;
using Nethereum.RPC.Shh.DTOs;
using Nethereum.RPC.Shh.KeyPair;
using Nethereum.RPC.Shh.SymKey;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class ShhPostTester : RPCRequestTester<string>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnResultUsingAsymKey()
        {
            var shhNewKeyPair = new ShhPost(Client);
            var shhAddPrivateKey = new ShhAddPrivateKey(Client);

            var keyPair = await shhAddPrivateKey.SendRequestAsync(Settings.GetDefaultShhPrivateKey()).ConfigureAwait(false);
            //4 Bytes
            var topic = UTF8Encoding.ASCII.GetBytes("default_topic").Take(4).ToArray().ToHex(true);
            var payload = UTF8Encoding.ASCII.GetBytes("default_message").ToHex(true);
            var result = await shhNewKeyPair.SendRequestAsync(new Shh.DTOs.MessageInput
            {
                PubKey = Settings.GetDefaultShhPublicKey(),
                Ttl = 7,
                Topic = topic,
                PowTarget = 2.1,
                PowTime = 100,
                Payload = payload,
                Sig = keyPair
            }).ConfigureAwait(false);
            Assert.NotNull(result);
        }

        [Fact]
        public async void ShouldReturnResultUsingSymKey()
        {
            var shhNewKeyPair = new ShhPost(Client);
            var shhSymKey = new ShhSymKey(Client);

            var symKeyId = await shhSymKey.AddSymKey.SendRequestAsync(Settings.GetDefaultShhPrivateKey()).ConfigureAwait(false); 
            //4 Bytes
            var topic = UTF8Encoding.ASCII.GetBytes("default_topic").Take(4).ToArray().ToHex(true);
            var payload = UTF8Encoding.ASCII.GetBytes("default_message").ToHex(true);
            var result = await shhNewKeyPair.SendRequestAsync(new Shh.DTOs.MessageInput
            {
                Ttl = 7,
                Topic = topic,
                PowTarget = 2.1,
                PowTime = 100,
                Payload = payload,
                SymKeyID = symKeyId
            }).ConfigureAwait(false);
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            return string.Empty;
        }

        public override Type GetRequestType()
        {
            return typeof(ShhNewKeyPair);
        }
    }
}