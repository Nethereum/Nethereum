using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Uncles;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetUncleByBlockNumberAndIndexTester : RPCRequestTester<BlockWithTransactionHashes>, IRPCRequestTester
    {
        public EthGetUncleByBlockNumberAndIndexTester():base(TestSettingsCategory.live)
        {
        }

        [Fact]
        public async void ShoulRetrieveUncleWithTransactionHashes()
        {
            var uncle = await ExecuteAsync();
            Assert.NotNull(uncle);
        }

        public override async Task<BlockWithTransactionHashes> ExecuteAsync(IClient client)
        {
            //https://etherscan.io/block/668
            var ethGetUncleByBlockNumberAndIndex = new EthGetUncleByBlockNumberAndIndex(client);
            return
                await
                    ethGetUncleByBlockNumberAndIndex.SendRequestAsync(
                        new BlockParameter(668), new HexBigInteger(0));
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetUncleByBlockNumberAndIndex);
        }
    }
}