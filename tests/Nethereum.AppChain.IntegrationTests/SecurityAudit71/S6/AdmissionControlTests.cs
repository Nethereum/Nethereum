using System.Numerics;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    [Collection("S6")]
    public class AdmissionControlTests
    {
        private const string Recipient = "0x0000000000000000000000000000000000000001";
        private readonly ITestOutputHelper _out;

        public AdmissionControlTests(ITestOutputHelper output) { _out = output; }

        private static async Task<string?> RefusalAsync(S6ProducerHost host, ISignedTransaction tx)
        {
            try
            {
                await host.Node.Sequencer!.SubmitTransactionAsync(tx);
                return null;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        [Fact]
        [Trait("Finding", "S6-I1")]
        public async Task Given_ASenderWhoseNonceWasConsumedByABlock_When_ItResubmitsTheSameNonce_Then_ItIsRefusedAsNonceTooLow()
        {
            await using var host = await S6ProducerHost.StartAsync();
            await host.Node.Sequencer!.SubmitTransactionAsync(host.Legacy(host.OwnerKey, Recipient, 0));
            await host.Node.Sequencer!.ProduceBlockAsync();

            var refusal = await RefusalAsync(host, host.Legacy(host.OwnerKey, Recipient, 0, data: "0x01", gasLimit: 30_000));

            _out.WriteLine("S6 refusal=" + refusal);
            Assert.Contains("nonce too low", refusal);
        }

        [Fact]
        [Trait("Finding", "S6-I1")]
        public async Task Given_ANonceGap_When_ItIsSubmitted_Then_ItIsRefusedAsNonceTooHigh()
        {
            await using var host = await S6ProducerHost.StartAsync();

            var refusal = await RefusalAsync(host, host.Legacy(host.OwnerKey, Recipient, 5));

            _out.WriteLine("S6 refusal=" + refusal);
            Assert.Contains("nonce too high", refusal);
        }

        [Fact]
        [Trait("Finding", "S6-I1")]
        public async Task Given_AGasLimitBelowTheIntrinsicGas_When_ItIsSubmitted_Then_ItIsRefused()
        {
            await using var host = await S6ProducerHost.StartAsync();

            var refusal = await RefusalAsync(host, host.Legacy(host.OwnerKey, Recipient, 0, gasLimit: 1_000));

            _out.WriteLine("S6 refusal=" + refusal);
            Assert.Contains("intrinsic gas too low", refusal);
        }

        [Fact]
        [Trait("Finding", "S6-I1")]
        public async Task Given_AValueTheSenderCannotCover_When_ItIsSubmitted_Then_ItIsRefusedAsInsufficientFunds()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var hex = new LegacyTransactionSigner().SignTransaction(
                EthECKey.GenerateKey().GetPrivateKey(), host.ChainId, Recipient, BigInteger.Pow(10, 30), 0, BigInteger.Zero, 21_000, "");
            var tx = TransactionFactory.CreateTransaction("0x" + hex.Replace("0x", ""));

            var refusal = await RefusalAsync(host, tx);

            _out.WriteLine("S6 refusal=" + refusal);
            Assert.Contains("insufficient funds", refusal);
        }

        [Fact]
        [Trait("Finding", "S6-I1")]
        public async Task Given_ASenderAtThePerSenderPendingLimit_When_ItSubmitsAnotherTransaction_Then_ItIsRefused()
        {
            await using var host = await S6ProducerHost.StartAsync();
            var limit = host.Node.Sequencer!.TxPool.MaxTxsPerSender;
            for (var nonce = 0; nonce < limit; nonce++)
                await host.Node.Sequencer!.SubmitTransactionAsync(host.Legacy(host.OwnerKey, Recipient, nonce));

            var refusal = await RefusalAsync(host, host.Legacy(host.OwnerKey, Recipient, limit));

            _out.WriteLine("S6 limit=" + limit + " refusal=" + refusal);
            Assert.Contains("per-sender limit", refusal);
        }

        [Fact]
        [Trait("Finding", "S6-J1")]
        public async Task Given_AnAllowListThatExcludesTheSender_When_ItSubmits_Then_TheSequencerRefusesIt()
        {
            var stranger = EthECKey.GenerateKey();
            await using var host = await S6ProducerHost.StartAsync(config =>
            {
                config.Consensus.Policy = Nethereum.AppChain.Sequencer.PolicyConfig.RestrictedAccess(
                    new List<string> { EthECKey.GenerateKey().GetPublicAddress() });
            });

            var refusal = await RefusalAsync(host, host.Legacy(stranger, Recipient, 0));

            _out.WriteLine("S6 refusal=" + refusal);
            Assert.Contains("allowed writers", refusal);
        }

        [Fact]
        [Trait("Finding", "S6-J2")]
        public async Task Given_AnAllowListThatExcludesTheSigner_When_EveryTransactionTypeIsValidated_Then_EachIsRefused()
        {
            var stranger = EthECKey.GenerateKey();
            await using var host = await S6ProducerHost.StartAsync(config =>
            {
                config.Consensus.Policy = Nethereum.AppChain.Sequencer.PolicyConfig.RestrictedAccess(
                    new List<string> { EthECKey.GenerateKey().GetPublicAddress() });
            });
            var enforcer = host.Node.Sequencer!.PolicyEnforcer;
            var chain = (EvmUInt256)host.ChainId;
            var zero = EvmUInt256.Zero;
            var gas = (EvmUInt256)200_000;

            var t2930 = new Transaction2930(chain, zero, zero, gas, Recipient, zero, "", new List<AccessListItem>());
            new TypeTransactionSigner<Transaction2930>().SignTransaction(stranger, t2930);
            var t7702 = new Transaction7702(chain, zero, zero, zero, gas, Recipient, zero, "", new List<AccessListItem>(), new List<Authorisation7702Signed>());
            new Transaction7702Signer().SignTransaction(stranger, t7702);
            var t4844 = new Transaction4844(chain, zero, zero, zero, gas, Recipient, zero, "", new List<AccessListItem>(), zero,
                new List<byte[]> { new byte[32] });
            new Transaction4844Signer().SignTransaction(stranger, t4844);

            var transactions = new ISignedTransaction[]
            {
                host.Legacy(stranger, Recipient, 0), t2930, host.Eip1559(stranger, Recipient, 0), t7702, t4844
            };
            foreach (var tx in transactions)
            {
                var verdict = await enforcer.ValidateTransactionAsync(tx);
                _out.WriteLine($"S6 {tx.GetType().Name} valid={verdict.IsValid}");
                Assert.False(verdict.IsValid);
            }
        }
    }
}
