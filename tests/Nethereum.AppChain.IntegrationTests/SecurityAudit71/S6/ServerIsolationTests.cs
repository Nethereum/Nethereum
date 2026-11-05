using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class ServerIsolationTests
    {
        private const string UnwiredKzgPrecompile = "0x000000000000000000000000000000000000000a";
        private const string Recipient = "0x0000000000000000000000000000000000000001";
        private readonly ITestOutputHelper _out;

        public ServerIsolationTests(ITestOutputHelper output) { _out = output; }

        [Theory]
        [InlineData(true, 128)]
        [InlineData(true, -1)]
        [InlineData(false, 128)]
        [InlineData(false, -1)]
        [Trait("Finding", "S6-I5")]
        public async Task Given_TheServerCompositionWithAStorageAndJournalSetting_When_AnUnwiredPrecompileTransactionIsPooledWithAnHonestOne_Then_ABlockIsProducedWithTheHonestTransaction(bool inMemory, int journalBlocks)
        {
            var dataDirectory = inMemory ? null : Path.Combine(Path.GetTempPath(), "s6-" + Guid.NewGuid().ToString("N"));
            try
            {
                await using var host = await S6ProducerHost.StartAsync(config =>
                {
                    config.Node.Storage.InMemory = inMemory;
                    if (dataDirectory != null) config.Node.Storage.DataDirectory = dataDirectory;
                    config.Node.Storage.JournalBlocks = journalBlocks;
                });
                var sequencer = host.Node.Sequencer!;
                var poison = host.Legacy(host.OwnerKey, UnwiredKzgPrecompile, 0, gasLimit: 500_000, data: "0x00");
                var honest = host.Legacy(EthECKey.GenerateKey(), Recipient, 0);
                await sequencer.SubmitTransactionAsync(poison);
                await sequencer.SubmitTransactionAsync(honest);

                var before = await sequencer.GetBlockNumberAsync();
                Exception? failure = null;
                try { await sequencer.ProduceBlockAsync(); }
                catch (Exception ex) { failure = ex; }
                var after = await sequencer.GetBlockNumberAsync();
                var honestMined = await host.Node.Bundle.Transactions.GetByHashAsync(honest.Hash) != null;

                _out.WriteLine($"S6 inMemory={inMemory} journal={journalBlocks} failure={failure?.GetType().Name} blocks {before}->{after} honestMined={honestMined}");
                Assert.Null(failure);
                Assert.True(honestMined);
            }
            finally
            {
                if (dataDirectory != null && Directory.Exists(dataDirectory))
                {
                    try { Directory.Delete(dataDirectory, true); } catch { }
                }
            }
        }
    }
}
