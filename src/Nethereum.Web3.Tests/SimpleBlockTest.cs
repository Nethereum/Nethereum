using System.Net.Configuration;
using System.Security.Principal;
using Nethereum.RPC.Tests;
using Nethereum.Web3;
using Xunit;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex;
using Nethereum.Hex.HexTypes;

namespace SimpleTests
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