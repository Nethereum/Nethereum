using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.Rsk.RPC.RskEth.DTOs;
using Xunit;

namespace Nethereum.Rsk.IntegrationTests
{
    public class GetBlockIntegrationTests
    {
        private string rskUrl = "https://public-node.rsk.co";

        [Fact]
        public async Task ShouldGetBlockWithHashesByNumber()
        {
            var web3 = new Web3Rsk(rskUrl);
            var block = await web3.RskEth.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new HexBigInteger("0x143828")).ConfigureAwait(false);
            Assert.Equal(59240000, block.GetMinimumGasPriceAsBigInteger());
        }

        [Fact]
        public async Task ShouldGetBlockWithTransactionsByNumber()
        {
            var web3 = new Web3Rsk(rskUrl);
            var block = await web3.RskEth.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger("0x143828")).ConfigureAwait(false);
            Assert.Equal(59240000, block.GetMinimumGasPriceAsBigInteger());
        }

        [Fact]
        public async Task ShouldGetBlockWithHashesByHash()
        {
            var web3 = new Web3Rsk(rskUrl);
            var block = await web3.RskEth.GetBlockWithTransactionsHashesByHash.SendRequestAsync("0x6cd513001e2d9c79847c797712407b9215e0fc74a4f0b368a42b3bc1a831bb57").ConfigureAwait(false);
            Assert.Equal(59240000, block.GetMinimumGasPriceAsBigInteger());
        }

        [Fact]
        public async Task ShouldGetBlockWithTransactionsByHash()
        {
            var web3 = new Web3Rsk(rskUrl);
            var block = await web3.RskEth.GetBlockWithTransactionsByHash.SendRequestAsync("0x6cd513001e2d9c79847c797712407b9215e0fc74a4f0b368a42b3bc1a831bb57").ConfigureAwait(false);
            Assert.Equal(59240000, block.GetMinimumGasPriceAsBigInteger());
        }

       
    }
}
