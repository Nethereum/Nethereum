using Nethereum.AppChain.Anchoring.Messaging;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class FixAttackAnalysisTests
    {
        private const string Recipient = "0x0000000000000000000000000000000000000001";
        private const string InfiniteLoopInitCode = "5b600056";
        private readonly ITestOutputHelper _out;

        public FixAttackAnalysisTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        [Trait("Finding", "S6-02")]
        public async Task Given_OneTransactionDeclaringTheWholeBlockGasLimitAndTenFeeFreeHonestOnes_When_ABlockIsProduced_Then_EveryHonestTransactionIsInTheSameBlock()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var sequencer = host.Node.Sequencer!;
            var blockGasLimit = (await host.Node.Bundle.Blocks.GetLatestAsync())!.GasLimit;
            var attacker = host.Legacy(EthECKey.GenerateKey(), Recipient, 0, gasLimit: blockGasLimit);
            var honest = Enumerable.Range(0, 10)
                .Select(_ => host.Legacy(EthECKey.GenerateKey(), Recipient, 0))
                .ToList();
            await sequencer.SubmitTransactionAsync(attacker);
            foreach (var tx in honest) await sequencer.SubmitTransactionAsync(tx);

            await sequencer.ProduceBlockAsync();

            var mined = 0;
            foreach (var tx in honest)
                if (await host.Node.Bundle.Transactions.GetByHashAsync(tx.Hash) != null) mined++;
            var attackerMined = await host.Node.Bundle.Transactions.GetByHashAsync(attacker.Hash) != null;
            var pooled = await sequencer.TxPool.GetPendingCountAsync();
            _out.WriteLine($"S6 honestMined={mined} attackerMined={attackerMined} pooled={pooled}");
            Assert.Equal(10, mined);
            Assert.Equal(0, pooled);
        }

        [Fact]
        [Trait("Finding", "S6-02")]
        public async Task Given_TransactionsWhoseActualGasFillsTheBlock_When_BlocksAreProduced_Then_TheOnesThatDoNotFitAreDeferredWithTheirSenderLaterTransactionsAndMinedNext()
        {
            await using var host = await S6ProducerHost.StartAsync(config =>
                config.Node.Producer.BlockGasLimit = 1_000_000);
            var sequencer = host.Node.Sequencer!;
            var burnA = host.Legacy(EthECKey.GenerateKey(), "", 0, gasLimit: 400_000, data: InfiniteLoopInitCode);
            var burnB = host.Legacy(EthECKey.GenerateKey(), "", 0, gasLimit: 400_000, data: InfiniteLoopInitCode);
            var lateKey = EthECKey.GenerateKey();
            var burnC = host.Legacy(lateKey, "", 0, gasLimit: 400_000, data: InfiniteLoopInitCode);
            var afterC = host.Legacy(lateKey, Recipient, 1);
            foreach (var tx in new[] { burnA, burnB, burnC, afterC }) await sequencer.SubmitTransactionAsync(tx);

            await sequencer.ProduceBlockAsync();
            var firstBlockMined = await CountMinedAsync(host, burnA, burnB, burnC, afterC);
            var pooledAfterFirst = await sequencer.TxPool.GetPendingCountAsync();

            await sequencer.ProduceBlockAsync();
            var secondBlockMined = await CountMinedAsync(host, burnA, burnB, burnC, afterC);
            var pooledAfterSecond = await sequencer.TxPool.GetPendingCountAsync();

            var latest = await host.Node.Bundle.Blocks.GetLatestAsync();
            _out.WriteLine($"S6 latest={latest!.BlockNumber} gasLimit={latest.GasLimit} gasUsed={latest.GasUsed} baseFee={latest.BaseFee}");
            _out.WriteLine($"S6 first mined={firstBlockMined} pooled={pooledAfterFirst}; second mined={secondBlockMined} pooled={pooledAfterSecond}");
            Assert.Equal(2, firstBlockMined);
            Assert.Equal(2, pooledAfterFirst);
            Assert.Equal(4, secondBlockMined);
            Assert.Equal(0, pooledAfterSecond);
        }

        [Fact]
        [Trait("Finding", "S6-07")]
        public async Task Given_AFeeFreeChainAndABlockMoreThanHalfFull_When_AZeroPriceHonestTransactionIsSubmitted_Then_TheBaseFeeIsStillZeroAndItIsMined()
        {
            await using var host = await S6ProducerHost.StartAsync(config =>
                config.Node.Producer.BlockGasLimit = 1_000_000);
            var sequencer = host.Node.Sequencer!;
            await sequencer.SubmitTransactionAsync(host.Legacy(EthECKey.GenerateKey(), "", 0, gasLimit: 400_000, data: InfiniteLoopInitCode));
            await sequencer.SubmitTransactionAsync(host.Legacy(EthECKey.GenerateKey(), "", 0, gasLimit: 400_000, data: InfiniteLoopInitCode));
            await sequencer.ProduceBlockAsync();
            var honest = host.Legacy(EthECKey.GenerateKey(), Recipient, 0);
            await sequencer.SubmitTransactionAsync(honest);

            await sequencer.ProduceBlockAsync();

            var latest = await host.Node.Bundle.Blocks.GetLatestAsync();
            _out.WriteLine($"S6 baseFee={latest!.BaseFee} honestMined={await host.Node.Bundle.Transactions.GetByHashAsync(honest.Hash) != null}");
            Assert.Equal(Nethereum.Util.EvmUInt256.Zero, latest.BaseFee);
            Assert.NotNull(await host.Node.Bundle.Transactions.GetByHashAsync(honest.Hash));
        }

        [Fact]
        [Trait("Finding", "S6-05")]
        public async Task Given_TwoHubMessagesAcrossTwoBlocksAndNoMessageExecutor_When_BlocksAreProduced_Then_BothAreRecordedAsNotExecutedAndTheQueueIsNotWedged()
        {
            await using var host = await S6ProducerHost.StartAsync(config => config.Messaging.Enabled = true);
            var queue = host.Node.MessageQueue!;
            queue.Enqueue(Message(1));
            await host.Node.Sequencer!.ProduceBlockAsync();
            queue.Enqueue(Message(2));
            await host.Node.Sequencer!.ProduceBlockAsync();

            var first = await host.Node.MessageResultStore.GetByMessageIdAsync(77, 1);
            var second = await host.Node.MessageResultStore.GetByMessageIdAsync(77, 2);
            _out.WriteLine($"S6 queued={queue.Count} first={first?.Success} second={second?.Success} leaves={host.Node.MessageAccumulator.GetLeafCount(77)}");
            Assert.Equal(0, queue.Count);
            Assert.False(first!.Success);
            Assert.False(second!.Success);
            Assert.Equal(2, host.Node.MessageAccumulator.GetLeafCount(77));
        }

        private static MessageInfo Message(ulong id) => new MessageInfo
        {
            MessageId = id,
            SourceChainId = 77,
            TargetChainId = 0,
            Sender = Recipient,
            Target = Recipient,
            Data = new byte[] { 1 }
        };

        private static async Task<int> CountMinedAsync(S6ProducerHost host, params Nethereum.Model.ISignedTransaction[] transactions)
        {
            var mined = 0;
            foreach (var tx in transactions)
                if (await host.Node.Bundle.Transactions.GetByHashAsync(tx.Hash) != null) mined++;
            return mined;
        }
    }
}
