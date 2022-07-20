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
            var account = new Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7", 1);
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
                .SignTransactionAsync(transfer).ConfigureAwait(false);

            Assert.Equal("f8a201646494de0b295669a9fd93d5f28d9ec85e40f4cb697bae80b844a9059cbb00000000000000000000000012890d2cce102216644c59dae5baed380d84830c000000000000000000000000000000000000000000000000000000000000000a25a0a0800bb95232492a24594b9c45ba3a77823e4c136b04187d9354038cd5bee76da00f54518f9a2d370b791a524a655cc16c68d5a676d93c504c1e6fc304712811f1", signedMessage);
        }
    }
}