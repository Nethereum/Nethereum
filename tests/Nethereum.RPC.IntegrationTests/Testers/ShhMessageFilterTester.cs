using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh;
using Nethereum.RPC.Shh.DTOs;
using Nethereum.RPC.Shh.KeyPair;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class ShhMessageFilterTester : RPCRequestTester<string>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnMessageFilterId()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var keyPair = new ShhKeyPair(Client);
            var messageFilter = new ShhMessageFilter(Client);
            var keyPairId = await keyPair.NewKeyPair.SendRequestAsync();
            var messageFilterId = await messageFilter.NewMessageFilter.SendRequestAsync(new Shh.DTOs.MessageFilterInput
            {
                Topics = new string[] { "0x64656661" },
                PrivateKeyID = keyPairId
            });
            var deleteMessageFilterResult = await messageFilter.DeleteMessageFilter.SendRequestAsync(messageFilterId);
            return messageFilterId;
        }

        public override Type GetRequestType()
        {
            return typeof(ShhNewKeyPair);
        }
    }

    public class ShhDeleteMessageFilterTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnResult()
        {
            var result = await ExecuteAsync();
            Assert.True(result);
        }

        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var keyPair = new ShhKeyPair(Client);
            var messageFilter = new ShhMessageFilter(Client);
            var keyPairId = await keyPair.NewKeyPair.SendRequestAsync();
            var messageFilterId = await messageFilter.NewMessageFilter.SendRequestAsync(new Shh.DTOs.MessageFilterInput
            {
                Topics = new string[] { "0x64656661" },
                PrivateKeyID = keyPairId
            });
            var deleteMessageFilterResult = await messageFilter.DeleteMessageFilter.SendRequestAsync(messageFilterId);
            return deleteMessageFilterResult;
        }

        public override Type GetRequestType()
        {
            return typeof(ShhNewKeyPair);
        }
    }

    public class ShhGetFilterMessagesTester : RPCRequestTester<ShhMessage[]>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnShhMessage()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }

        public override async Task<ShhMessage[]> ExecuteAsync(IClient client)
        {
            var post = new ShhPost(Client);
            var keyPair = new ShhKeyPair(Client);
            var messageFilter = new ShhMessageFilter(Client);
            var keyPairId = await keyPair.AddPrivateKey.SendRequestAsync(Settings.GetDefaultShhPrivateKey());
            var topic = "0x64656661";
            var payload = "0x64656661";
            var messageFilterId = await messageFilter.NewMessageFilter.SendRequestAsync(new Shh.DTOs.MessageFilterInput
            {
                Topics = new string[] { topic },
                PrivateKeyID = keyPairId
            });

            await post.SendRequestAsync(new Shh.DTOs.MessageInput
            {
                PubKey = Settings.GetDefaultShhPublicKey(),
                Ttl = 7,
                Topic = topic,
                PowTarget = 2.1,
                PowTime = 100,
                Payload = payload,
                Sig = keyPairId
            });

            var messages = await messageFilter.GetFilterMessages.SendRequestAsync(messageFilterId);

            var deleteMessageFilterResult = await messageFilter.DeleteMessageFilter.SendRequestAsync(messageFilterId);
            return messages;
        }

        public override Type GetRequestType()
        {
            return typeof(ShhNewKeyPair);
        }
    }
}