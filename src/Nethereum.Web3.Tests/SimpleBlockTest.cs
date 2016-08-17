using Nethereum.Hex.HexTypes;
using Xunit;

namespace Nethereum.Web3.Tests
{
    public class SimpleBlockTest
    {
        
        [Fact]
        public async void GetBlock()
        {
            var web3 = new Web3(ClientFactory.GetClient());
            var block = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new HexBigInteger(1139657));
            var transaction =
                await
                    web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(
                        "0x9122a4bba873e30c9c6e71481bd60ef61f559f60e26e50a38272f3324b7befca");

            var receipt = await
                web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(
                    "0x9122a4bba873e30c9c6e71481bd60ef61f559f60e26e50a38272f3324b7befca");

            Assert.NotNull(receipt);

        }
    }
}