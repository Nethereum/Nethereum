using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class TransactionTests
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public TransactionTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldReceiveTheTransactionHash()
        {

            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var toAddress = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";
            var receipt = await web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(toAddress, 1.11m, gasPriceGwei: 2).ConfigureAwait(false);
            var tran = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(receipt.TransactionHash);
            Assert.NotNull(tran.TransactionHash);
            var blockWithTransactions =
                await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(receipt.BlockNumber);
            foreach (var transaction in blockWithTransactions.Transactions)
            {
                        Assert.NotNull(transaction.TransactionHash);
            }
        }

    }
}