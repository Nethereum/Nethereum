using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class DeferredTransactionTests
    {
        private const string Recipient = "0x0000000000000000000000000000000000000001";
        private readonly ITestOutputHelper _out;

        public DeferredTransactionTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        [Trait("Finding", "S6-I4")]
        public async Task Given_SeventyTransactionsThatTogetherExceedTheBlockSizeBudget_When_BlocksAreProduced_Then_TheOverflowIsDeferredOnceAndMinedInTheNextBlock()
        {
            await using var host = await S6ProducerHost.StartAsync(config =>
                config.Node.Producer.BlockGasLimit = 4_000_000_000L);
            var sequencer = host.Node.Sequencer!;
            _out.WriteLine("S6 genesisGasLimit=" + (await host.Node.Bundle.Blocks.GetLatestAsync())!.GasLimit);
            var data = "0x" + new string((char)48, 2 * 120_000);
            var transactions = Enumerable.Range(0, 70)
                .Select(_ => host.Eip1559(EthECKey.GenerateKey(), Recipient, 0, gasLimit: 16_700_000, data: data))
                .ToList();
            foreach (var tx in transactions) await sequencer.SubmitTransactionAsync(tx);

            await sequencer.ProduceBlockAsync();
            var pooledAfterFirst = await sequencer.TxPool.GetPendingCountAsync();
            var minedAfterFirst = 0;
            foreach (var tx in transactions)
                if (await host.Node.Bundle.Transactions.GetByHashAsync(tx.Hash) != null) minedAfterFirst++;

            await sequencer.ProduceBlockAsync();
            var pooledAfterSecond = await sequencer.TxPool.GetPendingCountAsync();
            var minedAfterSecond = 0;
            foreach (var tx in transactions)
                if (await host.Node.Bundle.Transactions.GetByHashAsync(tx.Hash) != null) minedAfterSecond++;

            _out.WriteLine($"S6 first: mined={minedAfterFirst} pooled={pooledAfterFirst}; second: mined={minedAfterSecond} pooled={pooledAfterSecond}");
            Assert.Equal(61, minedAfterFirst);
            Assert.Equal(9, pooledAfterFirst);
            Assert.Equal(70, minedAfterSecond);
            Assert.Equal(0, pooledAfterSecond);
        }
    }
}
