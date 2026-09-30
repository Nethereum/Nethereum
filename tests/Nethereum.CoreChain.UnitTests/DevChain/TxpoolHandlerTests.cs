using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC.TxPool.DTOs;
using Nethereum.Signer;
using Xunit;
using static Nethereum.CoreChain.UnitTests.AmsterdamBlockPipelineHarness;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class TxpoolHandlerTests
    {
        private const string Recipient = "0x00000000000000000000000000000000000abc";

        [Fact]
        public async Task Given_AnEmptyPool_When_AskedForTxpoolStatus_Then_PendingAndQueuedAreZero()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync();

            var response = await new TxpoolStatusHandler().HandleAsync(
                new RpcRequestMessage(1, "txpool_status"),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            var result = Assert.IsType<TxPoolStatusResponse>(response.ResultNewtonsoft);

            Assert.Equal(BigInteger.Zero, result.Pending.Value);
            Assert.Equal(BigInteger.Zero, result.Queued.Value);
        }

        [Fact]
        public async Task Given_AnEmptyPool_When_AskedForTxpoolContent_Then_PendingAndQueuedAreEmpty()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync();

            var response = await new TxpoolContentHandler().HandleAsync(
                new RpcRequestMessage(1, "txpool_content"),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            var result = Assert.IsType<TxPoolContentResponse>(response.ResultNewtonsoft);

            Assert.Empty(result.Pending);
            Assert.Empty(result.Queued);
        }

        private static ISignedTransaction SignedTransferFrom(BigInteger nonce) =>
            TransactionFactory.CreateTransaction(
                new LegacyTransactionSigner().SignTransaction(
                    PrivateKey.HexToByteArray(), ChainId, Recipient, 1, nonce, 1_000_000_000, 21_000, ""));

        [Fact]
        public async Task Given_ATransactionSubmittedWithoutMining_When_AskedForTxpoolStatus_Then_PendingIsOne()
        {
            using var node = DevChainNode.CreateInMemory(
                new DevChainConfig { ChainId = (int)ChainId, AutoMine = false });
            await node.StartAsync(new[] { SenderAddress }, BigInteger.Parse("1000000000000000000"));

            await node.SendTransactionAsync(SignedTransferFrom(nonce: 0));

            var response = await new TxpoolStatusHandler().HandleAsync(
                new RpcRequestMessage(1, "txpool_status"),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            var result = Assert.IsType<TxPoolStatusResponse>(response.ResultNewtonsoft);

            Assert.Equal(BigInteger.One, result.Pending.Value);
            Assert.Equal(BigInteger.Zero, result.Queued.Value);
        }

        [Fact]
        public async Task Given_ATransactionSubmittedWithoutMining_When_AskedForTxpoolContent_Then_ItAppearsUnderItsSenderAndNonce()
        {
            using var node = DevChainNode.CreateInMemory(
                new DevChainConfig { ChainId = (int)ChainId, AutoMine = false });
            await node.StartAsync(new[] { SenderAddress }, BigInteger.Parse("1000000000000000000"));

            await node.SendTransactionAsync(SignedTransferFrom(nonce: 0));

            var response = await new TxpoolContentHandler().HandleAsync(
                new RpcRequestMessage(1, "txpool_content"),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            var result = Assert.IsType<TxPoolContentResponse>(response.ResultNewtonsoft);

            var from = SenderAddress.ToLowerInvariant();
            Assert.True(result.Pending.ContainsKey(from));
            Assert.True(result.Pending[from].ContainsKey("0"));
            Assert.Equal(from, result.Pending[from]["0"].From.ToLowerInvariant());
            Assert.Empty(result.Queued);
        }

        [Fact]
        public async Task Given_ATransactionSubmittedWithoutMining_When_AskedForTxpoolContentFrom_Then_ItIsFilteredToThatSender()
        {
            using var node = DevChainNode.CreateInMemory(
                new DevChainConfig { ChainId = (int)ChainId, AutoMine = false });
            await node.StartAsync(new[] { SenderAddress }, BigInteger.Parse("1000000000000000000"));

            await node.SendTransactionAsync(SignedTransferFrom(nonce: 0));

            var response = await new TxpoolContentFromHandler().HandleAsync(
                new RpcRequestMessage(1, "txpool_contentFrom", new object[] { SenderAddress }),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            var result = Assert.IsType<TxPoolContentFromResponse>(response.ResultNewtonsoft);

            Assert.True(result.Pending.ContainsKey("0"));
            Assert.Empty(result.Queued);
        }

        [Fact]
        public async Task Given_ATransactionSubmittedWithoutMining_When_AskedForTxpoolContent_Then_TheEntryCarriesTheFullTransactionBody()
        {
            using var node = DevChainNode.CreateInMemory(
                new DevChainConfig { ChainId = (int)ChainId, AutoMine = false });
            await node.StartAsync(new[] { SenderAddress }, BigInteger.Parse("1000000000000000000"));

            var signedTx = SignedTransferFrom(nonce: 0);
            await node.SendTransactionAsync(signedTx);

            var response = await new TxpoolContentHandler().HandleAsync(
                new RpcRequestMessage(1, "txpool_content"),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            var result = Assert.IsType<TxPoolContentResponse>(response.ResultNewtonsoft);

            var from = SenderAddress.ToLowerInvariant();
            var info = result.Pending[from]["0"];

            Assert.Equal(BigInteger.One, info.Value.Value);
            Assert.Equal(new BigInteger(21_000), info.Gas.Value);
            Assert.Equal(BigInteger.Zero, info.Nonce.Value);
        }
    }
}
