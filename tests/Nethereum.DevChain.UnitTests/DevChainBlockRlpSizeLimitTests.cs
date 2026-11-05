using System.Numerics;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Rpc;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevChain.UnitTests
{
    public class DevChainBlockRlpSizeLimitTests
    {
        private const int TransactionCount = 100;
        private const int CalldataBytes = 100_000;

        private static ISignedTransaction Padded(EthECKey sender, BigInteger chainId, int nonce)
        {
            var tx = new Transaction1559(
                chainId: chainId, nonce: nonce, maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(10_000_000_000), gasLimit: new BigInteger(1_200_000),
                receiverAddress: "0x000000000000000000000000000000000000dEaD",
                amount: BigInteger.Zero, data: new string('0', CalldataBytes * 2), accessList: null);
            var signature = sender.SignAndCalculateYParityV(tx.RawHash);
            tx.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });
            return tx;
        }

        [Fact]
        public async Task Given_OsakaPoolLargerThanTheBlockRlpLimit_When_ABlockIsMined_Then_ItFitsTheLimitAndTheRestStayPending()
        {
            var config = DevChainConfig.Default;
            config.Hardfork = "osaka";
            config.AutoMine = false;
            config.BlockGasLimit = 500_000_000;
            config.MaxTransactionsPerBlock = TransactionCount;
            var sender = EthECKey.GenerateKey();
            using var node = new DevChainNode(config);
            await node.StartAsync(new[] { sender });
            for (var nonce = 0; nonce < TransactionCount; nonce++)
                await node.SendTransactionAsync(Padded(sender, config.ChainId, nonce));
            Assert.Equal(TransactionCount, node.GetPendingTransactionCount());

            await node.MineBlockAsync();

            var produced = node.BlockManager.LastBlockProductionResult;
            var included = produced.IncludedTransactions.Count;
            var size = BlockHeaderExtensions.CalculateFullBlockSize(
                produced.Header, produced.IncludedTransactions, new List<BlockHeader>(), new List<Withdrawal>());
            Assert.True(size <= BlockBodyValidator.MaximumRlpBlockSize,
                $"block of {included} transactions is {size} bytes of RLP, over the {BlockBodyValidator.MaximumRlpBlockSize} limit");
            Assert.True(included > 0 && included < TransactionCount);
            Assert.Equal(TransactionCount - included, node.GetPendingTransactionCount());
        }
    }
}
