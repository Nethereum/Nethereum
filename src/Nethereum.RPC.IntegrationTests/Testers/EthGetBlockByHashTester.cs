using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetBlockByHashTester : RPCRequestTester<BlockWithTransactions>, IRPCRequestTester
    {
        public EthGetBlockByHashTester() : base(TestSettingsCategory.hostedTestNet)
        {

        }

        [Fact]
        public async void ShouldReturnBlockWithHashes()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
            Assert.NotNull(result.Transactions.FirstOrDefault(x => x.TransactionHash == Settings.GetTransactionHash()));
        }

        public override async Task<BlockWithTransactions> ExecuteAsync(IClient client)
        {
            var ethGetBlockByHash = new EthGetBlockWithTransactionsByHash(client);
            return
                await
                    ethGetBlockByHash.SendRequestAsync(Settings.GetBlockHash());
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetBlockWithTransactionsByHash);
        }
    }
}