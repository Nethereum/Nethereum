using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class PoolAndOrderingTests
    {
        private const string Recipient = "0x0000000000000000000000000000000000000001";
        private readonly ITestOutputHelper _out;

        public PoolAndOrderingTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        [Trait("Finding", "S6-02")]
        public async Task Given_AFirstTransactionDeclaresTheWholeBlockGasLimit_When_ABlockIsProduced_Then_TheNextHonestTransactionIsNotSilentlyLost()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var sequencer = host.Node.Sequencer!;
            var blockGasLimit = (await host.Node.Bundle.Blocks.GetLatestAsync())!.GasLimit;
            var attacker = host.Legacy(EthECKey.GenerateKey(), Recipient, 0, gasLimit: blockGasLimit);
            var honest = host.Legacy(EthECKey.GenerateKey(), Recipient, 0);
            await sequencer.SubmitTransactionAsync(attacker);
            await Task.Delay(20);
            await sequencer.SubmitTransactionAsync(honest);

            await sequencer.ProduceBlockAsync();
            var honestMined = await host.Node.Bundle.Transactions.GetByHashAsync(honest.Hash);
            var honestStillPooled = await sequencer.TxPool.ContainsAsync(honest.Hash);
            var attackerMined = await host.Node.Bundle.Transactions.GetByHashAsync(attacker.Hash);

            _out.WriteLine($"S6 blockGasLimit={blockGasLimit} attackerMined={attackerMined != null} honestMined={honestMined != null} honestStillPooled={honestStillPooled}");
            Assert.True(honestMined != null || honestStillPooled);
        }

        [Fact]
        [Trait("Finding", "S6-I3")]
        public async Task Given_MoreTransactionsThanMaxTransactionsPerBlock_When_ABlockIsProduced_Then_NoMoreThanTheMaximumAreIncludedAndTheRestStayPooled()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var sequencer = host.Node.Sequencer!;
            var max = sequencer.Config.MaxTransactionsPerBlock;
            var hashes = new List<byte[]>();
            for (var i = 0; i < max + 20; i++)
            {
                var tx = host.Legacy(EthECKey.GenerateKey(), Recipient, 0);
                await sequencer.SubmitTransactionAsync(tx);
                hashes.Add(tx.Hash);
            }

            await sequencer.ProduceBlockAsync();
            var latest = await host.Node.Bundle.Blocks.GetLatestAsync();
            var mined = 0;
            foreach (var hash in hashes)
                if (await host.Node.Bundle.Transactions.GetByHashAsync(hash) != null) mined++;
            var pooled = await sequencer.TxPool.GetPendingCountAsync();

            _out.WriteLine($"S6 max={max} mined={mined} pooled={pooled} block={latest!.BlockNumber}");
            Assert.True(mined <= max);
            Assert.Equal(hashes.Count, mined + pooled);
        }

    }
}
