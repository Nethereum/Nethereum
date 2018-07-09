using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Extensions;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    public class SignOfflineTransactionTest
    {

        [Function("transfer", "bool")]
        public class TransferFunction : FunctionMessage
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
                Nonce = 1, //we set the nonce so it does not get the latest
                Gas = 100, //we set the gas so it does not try to estimate it
                GasPrice=100 // we set the gas price so it does not retrieve the latest averate
            };

            

            var signedMessage = await web3.Eth.GetContractHandler("0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe")
                .SignTransactionAsync(transfer);

            Assert.Equal("f8a201646494de0b295669a9fd93d5f28d9ec85e40f4cb697bae80b844a9059cbb00000000000000000000000012890d2cce102216644c59dae5baed380d84830c000000000000000000000000000000000000000000000000000000000000000a1ca0a928719a67ff346732bfacd82d8c3d5f50490f57a9edd0c92438714bd6815cd4a0713e0577939049551bf0d4f66bacd2cf4ac371daa16f904f57d804101dcc6ee7", signedMessage);
        }
    }
}