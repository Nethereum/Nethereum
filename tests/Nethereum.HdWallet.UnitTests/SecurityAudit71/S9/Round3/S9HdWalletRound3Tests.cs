using System;
using System.Collections.Generic;
using NBitcoin;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.HdWallet.UnitTests.SecurityAudit71.S9.Round3
{
    public class S9HdWalletRound3Tests
    {
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        [Trait("Rule", "S9-R3-30")]
        [Fact]
        public void Given_messy_but_valid_phrases_When_constructing_strictly_Then_the_seed_equals_the_canonical_seed_or_the_phrase_is_rejected()
        {
            var expected = new Mnemonic(Abandon).DeriveSeed("pw").ToHex();
            var differing = new List<string>();
            foreach (var messy in new[] { Abandon.ToUpperInvariant(), "  " + Abandon + "\n", Abandon.Replace(" ", "\t"), Abandon.Replace(" ", "   "), "Abandon " + Abandon.Substring(8) })
            {
                try
                {
                    if (new Wallet(messy, "pw").Seed != expected) differing.Add(messy);
                }
                catch (ArgumentException) { }
            }
            Assert.True(differing.Count == 0, string.Join("|", differing));
        }

        [Trait("Rule", "S9-R3-31")]
        [Fact]
        public void Given_a_phrase_with_uppercase_true_When_constructing_strictly_Then_it_derives_the_standard_wallet()
        {
            var words = new Mnemonic(Wordlist.English, WordCount.Twelve).Words;
            for (var attempt = 0; attempt < 200000; attempt++)
            {
                var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                var index = Array.FindIndex(mnemonic.Words, w => w == "true" || w == "false");
                if (index < 0) continue;
                var upper = (string[])mnemonic.Words.Clone();
                upper[index] = upper[index].ToUpperInvariant();
                var phrase = string.Join(" ", upper);
                var standard = new Wallet(string.Join(" ", mnemonic.Words), "").GetAddresses(1)[0];
                string strict;
                try { strict = new Wallet(phrase, "").GetAddresses(1)[0]; }
                catch (ArgumentException) { return; }
                Assert.Equal(standard, strict);
                return;
            }
        }
    }
}
