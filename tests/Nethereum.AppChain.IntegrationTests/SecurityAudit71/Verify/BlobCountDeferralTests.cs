using System.Numerics;
using Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.Verify
{
    public class BlobCountDeferralTests
    {
        private const string Recipient = "0x0000000000000000000000000000000000000001";
        private readonly ITestOutputHelper _out;

        public BlobCountDeferralTests(ITestOutputHelper output) { _out = output; }

        private static ISignedTransaction BlobTransaction(S6ProducerHost host, EthECKey key, int blobs)
        {
            var hashes = Enumerable.Range(0, blobs).Select(i =>
            {
                var h = new byte[32];
                h[0] = 0x01;
                h[31] = (byte)i;
                return h;
            }).ToList();
            var zero = EvmUInt256.Zero;
            var tx = new Transaction4844((EvmUInt256)host.ChainId, zero, zero, zero, (EvmUInt256)200_000, Recipient, zero, "",
                new List<AccessListItem>(), (EvmUInt256)1_000_000_000, hashes);
            new Transaction4844Signer().SignTransaction(key, tx);
            return tx;
        }

        [Fact]
        public async Task Given_ThePoolFilledWithBlobTransactionsThatCanNeverFit_When_BlocksAreProduced_Then_AnHonestTransactionIsStillAdmitted()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var sequencer = host.Node.Sequencer!;
            var admitted = 0;
            for (var i = 0; i < 20_000; i++)
            {
                try
                {
                    await sequencer.SubmitTransactionAsync(BlobTransaction(host, EthECKey.GenerateKey(), 7));
                    admitted++;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("full") || ex.Message.Contains("rejected"))
                {
                    break;
                }
            }

            for (var i = 0; i < 5; i++) await sequencer.ProduceBlockAsync();

            string? refusal = null;
            try { await sequencer.SubmitTransactionAsync(host.Legacy(host.OwnerKey, Recipient, 0)); }
            catch (InvalidOperationException ex) { refusal = ex.Message; }
            var pooled = await sequencer.TxPool.GetPendingCountAsync();
            _out.WriteLine($"VERIFY admitted={admitted} pooledAfterFiveBlocks={pooled} honestRefusal={refusal ?? "none"}");
            Assert.Null(refusal);
        }

        [Theory]
        [InlineData(7)]
        [InlineData(40)]
        public async Task Given_ABlobTransactionCarryingMoreBlobsThanTheBlockCanEverHold_When_BlocksAreProduced_Then_ItIsNotKeptInThePoolForever(int blobs)
        {
            await using var host = await S6ProducerHost.StartAsync();
            var sequencer = host.Node.Sequencer!;
            var tx = BlobTransaction(host, EthECKey.GenerateKey(), blobs);
            string? refusal = null;
            try { await sequencer.SubmitTransactionAsync(tx); }
            catch (InvalidOperationException ex) { refusal = ex.Message; }

            for (var i = 0; i < 5; i++) await sequencer.ProduceBlockAsync();

            var pooled = await sequencer.TxPool.GetPendingCountAsync();
            var mined = await host.Node.Bundle.Transactions.GetByHashAsync(tx.Hash) != null;
            _out.WriteLine($"VERIFY blobs={blobs} refusal={refusal ?? "none"} pooledAfterFiveBlocks={pooled} mined={mined}");
            Assert.True(refusal != null || pooled == 0, $"the {blobs}-blob transaction was admitted and is still pooled after five blocks");
        }
    }
}
