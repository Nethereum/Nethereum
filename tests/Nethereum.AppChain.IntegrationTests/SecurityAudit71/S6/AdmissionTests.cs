using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class AdmissionTests
    {
        private const string Recipient = "0x0000000000000000000000000000000000000001";
        private readonly ITestOutputHelper _out;

        public AdmissionTests(ITestOutputHelper output) { _out = output; }

        private static async Task<bool> AcceptedAsync(S6ProducerHost host, ISignedTransaction tx)
        {
            try
            {
                await host.Node.Sequencer!.SubmitTransactionAsync(tx);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                return false;
            }
        }

        [Fact]
        [Trait("Finding", "S6-I1")]
        public async Task Given_AFundedSenderSignsForAnotherChainId_When_ItIsSubmittedToTheSequencer_Then_ItIsRefusedAtAdmission()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var tx = host.Eip1559(host.OwnerKey, Recipient, 0, chainIdOverride: host.ChainId + 1);

            var accepted = await AcceptedAsync(host, tx);

            _out.WriteLine("S6 accepted=" + accepted);
            Assert.False(accepted);
        }

        [Fact]
        [Trait("Finding", "S6-I1")]
        public async Task Given_AWrongChainIdTransactionWasAdmitted_When_ABlockIsProduced_Then_ItIsNotIncluded()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var tx = host.Eip1559(host.OwnerKey, Recipient, 0, chainIdOverride: host.ChainId + 1);
            await AcceptedAsync(host, tx);
            var control = host.Legacy(EthECKey.GenerateKey(), Recipient, 0);
            await host.Node.Sequencer!.SubmitTransactionAsync(control);

            await host.Node.Sequencer!.ProduceBlockAsync();
            var included = await host.Node.Bundle.Transactions.GetByHashAsync(tx.Hash);

            _out.WriteLine("tx included=" + (included != null));
            Assert.NotNull(await host.Node.Bundle.Transactions.GetByHashAsync(control.Hash));
            Assert.Null(included);
        }

        [Fact]
        [Trait("Finding", "S6-I1")]
        public async Task Given_ATransactionLargerThanTheDevP2PAdmissionCap_When_ItIsSubmittedToTheSequencer_Then_ItIsRefusedAtAdmission()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var data = "0x" + new string('0', 2 * 1_000_000);
            var tx = host.Eip1559(host.OwnerKey, Recipient, 0, gasLimit: 70_000_000, data: data);

            var accepted = await AcceptedAsync(host, tx);

            _out.WriteLine("S6 accepted=" + accepted + " rlpBytes=" + tx.GetRLPEncoded().Length);
            Assert.False(accepted);
        }

        [Fact]
        [Trait("Finding", "S6-I1")]
        public async Task Given_AGasLimitAboveTheBlockGasLimit_When_ItIsSubmittedToTheSequencer_Then_ItIsRefusedAtAdmission()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var tx = host.Eip1559(host.OwnerKey, Recipient, 0, gasLimit: 9_000_000_000_000L);

            var accepted = await AcceptedAsync(host, tx);

            _out.WriteLine("S6 accepted=" + accepted);
            Assert.False(accepted);
        }

        [Fact]
        [Trait("Finding", "S6-I1b")]
        public async Task Given_ALegacyTransactionWithAHighSSignature_When_ItIsSubmittedToTheSequencer_Then_ItIsRefusedAtAdmission()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var original = host.Legacy(host.OwnerKey, Recipient, 0);
            var malleated = HighS(original);

            var accepted = await AcceptedAsync(host, malleated);

            _out.WriteLine("S6 accepted=" + accepted + " originalHash=" + original.Hash.ToHex() + " malleatedHash=" + malleated.Hash.ToHex());
            Assert.NotEqual(original.Hash.ToHex(), malleated.Hash.ToHex());
            Assert.False(accepted);
        }

        [Fact]
        [Trait("Finding", "S6-I1b")]
        public async Task Given_ALegacyTransactionWithAHighSSignature_When_ItWasAdmittedAndABlockIsProduced_Then_ItIsNotMinedUnderTheMalleatedHash()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var original = host.Legacy(host.OwnerKey, Recipient, 0);
            var malleated = HighS(original);
            await AcceptedAsync(host, malleated);
            var control = host.Legacy(EthECKey.GenerateKey(), Recipient, 0);
            await host.Node.Sequencer!.SubmitTransactionAsync(control);

            await host.Node.Sequencer!.ProduceBlockAsync();
            var minedOriginal = await host.Node.Bundle.Transactions.GetByHashAsync(original.Hash);
            var minedMalleated = await host.Node.Bundle.Transactions.GetByHashAsync(malleated.Hash);

            _out.WriteLine("original mined=" + (minedOriginal != null) + " malleated mined=" + (minedMalleated != null));
            Assert.NotNull(await host.Node.Bundle.Transactions.GetByHashAsync(control.Hash));
            Assert.Null(minedMalleated);
        }

        private static ISignedTransaction HighS(ISignedTransaction original)
        {
            var curveOrder = BigInteger.Parse("115792089237316195423570985008687907852837564279074904382605163141518161494337");
            var s = new BigInteger(original.Signature.S, isUnsigned: true, isBigEndian: true);
            var highS = curveOrder - s;
            var originalV = new BigInteger(original.Signature.V, isUnsigned: true, isBigEndian: true);
            var flippedV = (originalV - 35) % 2 == 0 ? originalV + 1 : originalV - 1;
            var v = flippedV.ToByteArray(isUnsigned: true, isBigEndian: true);
            var copy = TransactionFactory.CreateTransaction(original.GetRLPEncoded().ToHex().EnsureHexPrefix());
            copy.SetSignature(new Signature
            {
                R = original.Signature.R,
                S = highS.ToByteArray(isUnsigned: true, isBigEndian: true),
                V = v
            });
            return copy;
        }
    }
}
