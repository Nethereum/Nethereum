using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.CQS;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    public class SignOfflineTransactionTest
    {

        [Function("transfer", "bool")]
        public class TransferFunction : ContractMessage
        {
            [Parameter("address", "_to", 1)]
            public string To { get; set; }

            [Parameter("uint256", "_value", 2)]
            public int TokenAmount { get; set; }
        }


        [Fact]
        public async Task ShouldSignOfflineTransaction()
        {
            var account = new Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");
            var web3 = new Web3.Web3(account);
            var transfer = new TransferFunction()
            {
                To = "0x12890d2cce102216644c59daE5baed380d84830c",
                TokenAmount = 10,
                Nonce = 1,
            };

            var signedMessage = await web3.Eth.GetContractHandler("0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe")
                .SignTransactionAsync(transfer, false);

            Assert.Equal("f8a9018504a817c80082520894de0b295669a9fd93d5f28d9ec85e40f4cb697bae80b844a9059cbb00000000000000000000000012890d2cce102216644c59dae5baed380d84830c000000000000000000000000000000000000000000000000000000000000000a1ba052955ae4c9e47442c607fb627511ed9a98438aaa69cd86763c10e12353fd3d27a04967781092a6a9041277ae906d6125b89a04b20b109de73c7781a727fe76d926", signedMessage);
        }
    }
}