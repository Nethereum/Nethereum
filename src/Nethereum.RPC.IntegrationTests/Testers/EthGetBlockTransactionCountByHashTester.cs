using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetBlockTransactionCountByHashTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        public EthGetBlockTransactionCountByHashTester() : base(TestSettingsCategory.hostedTestNet)
        {

        }

        [Fact]
        public async void ShouldReturnTransactionCount()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
            //we have configured one transaction at least for this block
            Assert.True(result.Value > 0);
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var ethGetBlockTransactionCountByHash = new EthGetBlockTransactionCountByHash(client);
            return
                await
                    ethGetBlockTransactionCountByHash.SendRequestAsync(Settings.GetBlockHash());
        }

        public override Type GetRequestType()
        {
            return typeof (EthGetBlockTransactionCountByHash);
        }
    }
}