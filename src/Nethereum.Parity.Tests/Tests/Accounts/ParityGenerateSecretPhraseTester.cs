
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Accounts;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityGenerateSecretPhraseTester : RPCRequestTester<string>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldGenerateSecretePhrase()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var parityGenerateSecretPhrase = new ParityGenerateSecretPhrase(client);
            return await parityGenerateSecretPhrase.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityGenerateSecretPhrase);
        }
    }
}
        