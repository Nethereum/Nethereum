using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Tests.Testers;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetBalanceTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnBalanceBiggerThanZero()
        {
            var result = await ExecuteAsync();
            //Default account has balance
            Assert.True(result.Value > 0);
        }

        [Fact]  
        public async void ShouldReturnBalanceBiggerThanZeroForCurrentBlock()
        {
            var blockNumber = await (new EthBlockNumber(Client)).SendRequestAsync();
            var ethGetBalance = new EthGetBalance(Client);
            var result = await ethGetBalance.SendRequestAsync(Settings.GetDefaultAccount(), new BlockParameter(blockNumber));
            //Default account has balance
            Assert.True(result.Value > 0);
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var ethGetBalance = new EthGetBalance(client);
            return await ethGetBalance.SendRequestAsync(Settings.GetDefaultAccount());
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetBalance);
        }
    }
}

