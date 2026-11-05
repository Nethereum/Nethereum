using System;
using NBitcoin;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.HdWallet.UnitTests.SecurityAudit71.S9.Round6
{
    public class S9HdWalletRound6Tests
    {
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        [Trait("Rule", "S9-R6-4")]
        [Fact]
        public void Given_very_long_inputs_When_constructing_leniently_Then_they_are_accepted_and_strict_still_rejects()
        {
            foreach (var length in new[] { 4097, 200_000 })
            {
                var phrase = Abandon + new string(' ', length);
                Assert.Equal(new Mnemonic(phrase).DeriveSeed("pw").ToHex(), new Wallet(phrase, "pw", Wallet.DEFAULT_PATH, null, false).Seed);
                var pass = new string('p', length);
                Assert.Equal(new Mnemonic(Abandon).DeriveSeed(pass).ToHex(), new Wallet(Abandon, pass, Wallet.DEFAULT_PATH, null, false).Seed);
                Assert.ThrowsAny<ArgumentException>(() => new Wallet(phrase, "pw"));
                Assert.ThrowsAny<ArgumentException>(() => new Wallet(Abandon, pass));
            }
        }
    }
}
