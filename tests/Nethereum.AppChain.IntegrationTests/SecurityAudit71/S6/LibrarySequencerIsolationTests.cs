using System.Numerics;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.Builder;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class LibrarySequencerIsolationTests
    {
        private const string OperatorKey = "0x12345678901234567890123456789012345678901234567890123456789012ab";
        private const string UnwiredKzgPrecompile = "0x000000000000000000000000000000000000000a";
        private const string Recipient = "0x0000000000000000000000000000000000000001";
        private static readonly BigInteger GasPrice = 2_000_000_000;
        private readonly ITestOutputHelper _out;

        public LibrarySequencerIsolationTests(ITestOutputHelper output) { _out = output; }

        private static ISignedTransaction Signed(EthECKey key, BigInteger chainId, string to, int nonce)
        {
            var hex = new LegacyTransactionSigner().SignTransaction(
                key.GetPrivateKey().HexToByteArray(), chainId, to, BigInteger.Zero, nonce, GasPrice, 500_000, "00");
            return TransactionFactory.CreateTransaction(hex.EnsureHexPrefix());
        }

        [Fact]
        [Trait("Finding", "S6-I5")]
        public async Task Given_AnInMemoryAppChainBuilderSequencer_When_AnUnwiredPrecompileTransactionIsPooledWithAnHonestOne_Then_ABlockIsStillProducedWithTheHonestTransaction()
        {
            var poisonKey = EthECKey.GenerateKey();
            var honestKey = EthECKey.GenerateKey();
            await using var chain = await new AppChainBuilder("S6Library", 421701)
                .WithOperator(OperatorKey)
                .WithBlockTime(3_600_000)
                .WithPrefundedAddresses(new[] { poisonKey.GetPublicAddress(), honestKey.GetPublicAddress() })
                .BuildAsync();
            var poison = Signed(poisonKey, chain.ChainId, UnwiredKzgPrecompile, 0);
            var honest = Signed(honestKey, chain.ChainId, Recipient, 0);
            await chain.Sequencer.SubmitTransactionAsync(poison);
            await chain.Sequencer.SubmitTransactionAsync(honest);

            var blocksBefore = await chain.GetBlockNumberAsync();
            Exception? failure = null;
            try { await chain.ProduceBlockAsync(); }
            catch (Exception ex) { failure = ex; }
            var blocksAfter = await chain.GetBlockNumberAsync();

            _out.WriteLine($"S6 failure={failure?.GetType().Name}: {failure?.Message} blocks {blocksBefore}->{blocksAfter}");
            Assert.Null(failure);
            Assert.Equal(blocksBefore + 1, blocksAfter);
            Assert.NotNull(await chain.AppChain.Transactions.GetByHashAsync(honest.Hash));
        }
    }
}
