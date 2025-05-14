using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Chain;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionTypes;
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

        [Fact]
        public async void ShouldReceiveTheTransactionByHashPendingAndNullValuesDependingOnTrasactionType()
        {

            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var toAddress = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";
            var transactionHash = await web3.Eth.GetEtherTransferService()
                .TransferEtherAsync(toAddress, 1.11m, gasPriceGwei: 2).ConfigureAwait(false);
            var tran = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(transactionHash);
            Assert.NotNull(tran.TransactionHash);
            Assert.Null(tran.MaxFeePerGas);
            Assert.Null(tran.MaxPriorityFeePerGas);
            Assert.NotNull(tran.GasPrice);

            var transactionHash2 = await web3.Eth.GetEtherTransferService()
                .TransferEtherAsync(toAddress, 1.11m, maxFeePerGas: Web3.Web3.Convert.ToWei(2, Util.UnitConversion.EthUnit.Gwei), maxPriorityFee: Web3.Web3.Convert.ToWei(2, Util.UnitConversion.EthUnit.Gwei)).ConfigureAwait(false);
            var tran2 = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(transactionHash2);
            Assert.NotNull(tran2.TransactionHash);
            Assert.NotNull(tran2.MaxFeePerGas);
            Assert.NotNull(tran2.MaxPriorityFeePerGas);
            Assert.NotNull(tran2.GasPrice);

        }


        [Fact]
        public async void ShouldSendTrasactionBasedOnChainFeature()
        {

            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            ChainFeaturesService.Current.UpsertChainFeature(
                new ChainFeature()
                {
                    ChainName = "Nethereum Test Chain",
                    ChainId = 1337,
                    SupportEIP1559 = false
                });

            var toAddress = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";
            var tranHash = await web3.Eth.TransactionManager.SendTransactionAsync(new TransactionInput()
            {
                From = EthereumClientIntegrationFixture.AccountAddress,
                To = toAddress,
                Value = new HexBigInteger(100),
            }
            );
                
            var tran = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(tranHash);
            Assert.NotNull(tran.TransactionHash);
            Assert.Null(tran.MaxFeePerGas);
            Assert.Null(tran.MaxPriorityFeePerGas);
            Assert.NotNull(tran.GasPrice);

            ChainFeaturesService.Current.UpsertChainFeature(
                new ChainFeature()
                {
                    ChainName = "Nethereum Test Chain",
                    ChainId = 1337,
                    SupportEIP1559 = true
                });

            var tranHash2 = await web3.Eth.TransactionManager.SendTransactionAsync(new TransactionInput()
            {
                From = EthereumClientIntegrationFixture.AccountAddress,
                To = toAddress,
                Value = new HexBigInteger(100),
            }
            );

            var tran2 = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(tranHash2);
            Assert.NotNull(tran2.TransactionHash);
            Assert.NotNull(tran2.MaxFeePerGas);
            Assert.NotNull(tran2.MaxPriorityFeePerGas);
            Assert.NotNull(tran2.GasPrice);

            ChainFeaturesService.Current.TryRemoveChainFeature(1337);
            //Should default to 1559 when not feature is set

            var tranHash3 = await web3.Eth.TransactionManager.SendTransactionAsync(new TransactionInput()
            {
                From = EthereumClientIntegrationFixture.AccountAddress,
                To = toAddress,
                Value = new HexBigInteger(100),
            }
           );

            var tran3 = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(tranHash3);
            Assert.NotNull(tran3.TransactionHash);
            Assert.NotNull(tran3.MaxFeePerGas);
            Assert.NotNull(tran3.MaxPriorityFeePerGas);
            Assert.NotNull(tran3.GasPrice);
        }

        [Fact]
        public async void ShouldGetTransactionByHash()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var txnType2 = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync("0xe7bab1a12b9234a27a0f53f71d19bc0595f1ea2c8148f5d45edac76a4566e15b");
            var txnLegacy = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync("0x8751032c189f44478b13ca77834b6af3567ec3e014069450f17209ed0fd1a3c1");
            Assert.True(txnType2.Type.ToTransactionType() == TransactionType.EIP1559);
            Assert.True(txnLegacy.Type.ToTransactionType() == TransactionType.Legacy);

            Assert.True(txnType2.Is1559());
            Assert.True(txnLegacy.IsLegacy());
        }

    }
}