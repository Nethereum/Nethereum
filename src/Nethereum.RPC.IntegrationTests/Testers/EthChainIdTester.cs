using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthChainIdTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        public EthChainIdTester() :
            base(TestSettingsCategory.hostedTestNet)
        {
        }

        [Fact]
        public async void ShouldRetrieveChainId()
        {
            var chainId = await ExecuteAsync();
            Assert.True(chainId.Value > 0);
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var ethAccounts = new EthChainId(client);
            return await ethAccounts.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof (EthChainId);
        }
    }
}