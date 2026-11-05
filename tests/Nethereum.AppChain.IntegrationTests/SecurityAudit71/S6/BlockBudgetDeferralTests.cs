using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class BlockBudgetDeferralTests
    {
        private const string Recipient = "0x0000000000000000000000000000000000000001";
        private readonly ITestOutputHelper _out;

        public BlockBudgetDeferralTests(ITestOutputHelper output) { _out = output; }

        private static ISignedTransaction BlobTransaction(S6ProducerHost host, EthECKey key, int blobs, int nonce = 0)
        {
            var hashes = Enumerable.Range(0, blobs).Select(i =>
            {
                var h = new byte[32];
                h[0] = 0x01;
                h[31] = (byte)i;
                return h;
            }).ToList();
            var zero = EvmUInt256.Zero;
            var tx = new Transaction4844((EvmUInt256)host.ChainId, (EvmUInt256)nonce, zero, zero, (EvmUInt256)200_000, Recipient, zero, "",
                new List<AccessListItem>(), (EvmUInt256)1_000_000_000, hashes);
            new Transaction4844Signer().SignTransaction(key, tx);
            return tx;
        }

        [Theory]
        [InlineData(7)]
        [InlineData(40)]
        public async Task Given_AnOverCountBlobTransactionThatReachedThePoolBeforeTheAdmissionRule_When_ABlockIsProduced_Then_ItIsDroppedAndNotDeferred(int blobs)
        {
            await using var host = await S6ProducerHost.StartAsync();
            var sequencer = host.Node.Sequencer!;
            var tx = BlobTransaction(host, EthECKey.GenerateKey(), blobs);
            await sequencer.TxPool.AddAsync(tx);

            await sequencer.ProduceBlockAsync();

            var pooled = await sequencer.TxPool.GetPendingCountAsync();
            _out.WriteLine($"S6 blobs={blobs} pooledAfterOneBlock={pooled}");
            Assert.Equal(0, pooled);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        [InlineData(40)]
        public async Task Given_ABlobTransactionWhoseBlobCountIsOutsideOneToSix_When_ItIsSubmittedToTheSequencer_Then_ItIsRefusedAtAdmission(int blobs)
        {
            await using var host = await S6ProducerHost.StartAsync();
            var tx = BlobTransaction(host, EthECKey.GenerateKey(), blobs);

            var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
                () => host.Node.Sequencer!.SubmitTransactionAsync(tx));

            _out.WriteLine("S6 refusal=" + refusal.Message);
            Assert.Contains("blob count", refusal.Message);
        }

        [Fact]
        public async Task Given_ATransactionDeclaringMoreGasThanTheBlockGasLimitThatReachedThePool_When_ABlockIsProduced_Then_ItIsDroppedAndNotDeferred()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var sequencer = host.Node.Sequencer!;
            var blockGasLimit = (await host.Node.Bundle.Blocks.GetLatestAsync())!.GasLimit;
            var tx = host.Legacy(EthECKey.GenerateKey(), Recipient, 0, gasLimit: blockGasLimit + 1);
            await sequencer.TxPool.AddAsync(tx);

            await sequencer.ProduceBlockAsync();

            var pooled = await sequencer.TxPool.GetPendingCountAsync();
            _out.WriteLine($"S6 pooledAfterOneBlock={pooled}");
            Assert.Equal(0, pooled);
        }

        [Fact]
        public async Task Given_MoreMaximumBlobTransactionsThanOneBlockHolds_When_BlocksAreProduced_Then_TheOverflowIsDeferredAndMinedInLaterBlocks()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var sequencer = host.Node.Sequencer!;
            var transactions = Enumerable.Range(0, 4)
                .Select(nonce => BlobTransaction(host, host.OwnerKey, 6, nonce))
                .ToList();
            foreach (var tx in transactions) await sequencer.SubmitTransactionAsync(tx);

            await sequencer.ProduceBlockAsync();
            var pooledAfterFirst = await sequencer.TxPool.GetPendingCountAsync();
            for (var i = 0; i < 5; i++) await sequencer.ProduceBlockAsync();
            var pooledAfterSix = await sequencer.TxPool.GetPendingCountAsync();
            var mined = 0;
            foreach (var tx in transactions)
                if (await host.Node.Bundle.Transactions.GetByHashAsync(tx.Hash) != null) mined++;

            _out.WriteLine($"S6 pooledAfterFirst={pooledAfterFirst} pooledAfterSix={pooledAfterSix} mined={mined}");
            Assert.True(pooledAfterFirst > 0);
            Assert.Equal(0, pooledAfterSix);
            Assert.Equal(4, mined);
        }
    }
}
