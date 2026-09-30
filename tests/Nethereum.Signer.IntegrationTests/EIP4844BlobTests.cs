using System.Text;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.EVM.Precompiles.Kzg;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Signer.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EIP4844BlobTests
    {
        private readonly EthereumClientIntegrationFixture _fixture;

        public EIP4844BlobTests(EthereumClientIntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ShouldSendBlobTransactionToGeth()
        {
            var web3 = _fixture.GetWeb3();
            var account = EthereumClientIntegrationFixture.GetAccount();

            CkzgOperations.InitializeFromEmbeddedSetup();
            var kzg = new CkzgOperations();
            var builder = new BlobSidecarBuilder(kzg);

            var data = Encoding.UTF8.GetBytes("Hello Ethereum Blobs from Nethereum!");
            var (sidecar, versionedHashes) = builder.BuildFromData(data);

            Assert.Single(versionedHashes);
            Assert.Equal(0x01, versionedHashes[0][0]);

            var nonce = await web3.Eth.TransactionManager.Account.NonceService
                .GetNextNonceAsync().ConfigureAwait(false);
            var lastBlock = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest()).ConfigureAwait(false);
            var baseFee = lastBlock.BaseFeePerGas.Value;

            var tx = new Transaction4844(
                chainId: EthereumClientIntegrationFixture.ChainId,
                nonce: nonce,
                maxPriorityFeePerGas: 2000000000,
                maxFeePerGas: baseFee * 2 + 2000000000,
                gasLimit: 21000,
                receiverAddress: "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                amount: 0,
                data: null,
                accessList: null,
                maxFeePerBlobGas: 10000000000,
                blobVersionedHashes: versionedHashes);

            tx.Sidecar = sidecar;

            var signer = new Transaction4844Signer();
            signer.SignTransaction(
                new EthECKey(EthereumClientIntegrationFixture.AccountPrivateKey), tx);

            var rawTx = tx.GetRLPEncodedWithSidecar().ToHex();

            var txHash = await web3.Eth.Transactions.SendRawTransaction
                .SendRequestAsync(rawTx).ConfigureAwait(false);
            Assert.NotNull(txHash);

            var receipt = await web3.TransactionReceiptPolling
                .PollForReceiptAsync(txHash).ConfigureAwait(false);
            Assert.NotNull(receipt);
            Assert.Equal("0x1", receipt.Status.HexValue);

            Assert.NotNull(receipt.BlobGasUsed);
            Assert.True(receipt.BlobGasUsed.Value > 0);

            var txFromChain = await web3.Eth.Transactions.GetTransactionByHash
                .SendRequestAsync(txHash).ConfigureAwait(false);
            Assert.NotNull(txFromChain.MaxFeePerBlobGas);
            Assert.NotNull(txFromChain.BlobVersionedHashes);
            Assert.Single(txFromChain.BlobVersionedHashes);
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip4844-blob-tx", "Send blob transaction via high-level API")]
        public async Task ShouldSendBlobViaHighLevelApi()
        {
            var web3 = _fixture.GetWeb3();

            CkzgOperations.InitializeFromEmbeddedSetup();
            var kzg = new CkzgOperations();

            var data = Encoding.UTF8.GetBytes("High-level blob API test!");
            var txHash = await ((AccountSignerTransactionManager)web3.TransactionManager)
                .SendBlobTransactionAsync(data, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", kzg)
                .ConfigureAwait(false);

            Assert.NotNull(txHash);

            var receipt = await web3.TransactionReceiptPolling
                .PollForReceiptAsync(txHash).ConfigureAwait(false);
            Assert.NotNull(receipt);
            Assert.Equal("0x1", receipt.Status.HexValue);
            Assert.True(receipt.BlobGasUsed.Value > 0);
        }
    }
}
