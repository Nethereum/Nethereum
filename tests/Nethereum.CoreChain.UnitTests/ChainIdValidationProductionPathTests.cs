using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class ChainIdValidationProductionPathTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";

        private static readonly BigInteger NodeChainId = 1337;

        private static readonly BigInteger WrongChainId = 999;

        private static readonly LegacyTransactionSigner Signer = new();

        private static TransactionProcessor BuildProcessor(out ChainConfig config)
        {
            var stateStore = new InMemoryStateStore();
            var blockStore = new InMemoryBlockStore();
            config = new ChainConfig { ChainId = NodeChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            return new TransactionProcessor(stateStore, blockStore, config, txVerifier);
        }

        private static ISignedTransaction SignForChain(BigInteger chainId, BigInteger nonce)
        {
            var signedTxHex = Signer.SignTransaction(
                PrivateKey.HexToByteArray(),
                chainId,
                RecipientAddress,
                1000,
                nonce,
                1,
                21_000,
                "");
            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        [Fact]
        public async Task Transaction_SignedForWrongChain_IsRejectedByProductionPath()
        {
            var processor = BuildProcessor(out var config);
            var wrongChainTx = SignForChain(WrongChainId, nonce: 0);
            var blockContext = BlockContext.FromConfig(config, blockNumber: 1, timestamp: 1_700_000_000);

            var result = await processor.ExecuteTransactionAsync(
                wrongChainTx, blockContext, txIndex: 0, cumulativeGasUsed: 0);

            Assert.True(result.Skipped, "a transaction signed for a different chain id must not execute");
            Assert.False(result.Success);
            Assert.Contains("INVALID_CHAINID", result.RevertReason ?? "");
        }

        [Fact]
        public async Task Transaction_SignedForCorrectChain_IsNotRejectedOnChainId()
        {
            var processor = BuildProcessor(out var config);
            var correctChainTx = SignForChain(NodeChainId, nonce: 0);
            var blockContext = BlockContext.FromConfig(config, blockNumber: 1, timestamp: 1_700_000_000);

            var result = await processor.ExecuteTransactionAsync(
                correctChainTx, blockContext, txIndex: 0, cumulativeGasUsed: 0);

            Assert.DoesNotContain("INVALID_CHAINID", result.RevertReason ?? "");
        }

        [Fact]
        public void Given_AnEip155LegacyTransaction_When_Mapped_Then_ItsDeclaredChainIdIsPopulated()
        {
            var ctx = MapForThisChain(SignForChain(NodeChainId, nonce: 0));

            Assert.Equal((Nethereum.Util.EvmUInt256)NodeChainId, ctx.DeclaredChainId);
        }

        [Fact]
        public void Given_APlainPreEip155LegacyTransaction_When_Mapped_Then_ItDeclaresNoChainIdAtAll()
        {
            var tx = new LegacyTransaction(
                nonce: new byte[] { },
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { 0x0a },
                data: System.Array.Empty<byte>());

            Assert.Null(MapForThisChain(tx).DeclaredChainId);
        }

        private static Nethereum.EVM.TransactionExecutionContext MapForThisChain(ISignedTransaction tx)
            => Nethereum.EVM.Execution.TransactionContextFactory.From(
                tx, RecipientAddress, new BlockContext { ChainId = NodeChainId }, null);
    }
}
