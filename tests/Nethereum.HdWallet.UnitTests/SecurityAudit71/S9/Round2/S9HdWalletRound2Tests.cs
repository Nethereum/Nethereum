using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.HdWallet.UnitTests.SecurityAudit71.S9.Round2
{
    public class S9HdWalletRound2Tests
    {
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        private class FixedRandom : IRandom
        {
            public int Calls;

            public void GetBytes(byte[] output)
            {
                Calls++;
                for (var i = 0; i < output.Length; i++) output[i] = 7;
            }

            public void GetBytes(Span<byte> output)
            {
                Calls++;
                output.Fill(7);
            }
        }

        [Trait("Rule", "S9-R2-50")]
        [Fact]
        public void Given_non_standard_mnemonics_When_constructing_leniently_Then_the_seed_equals_what_nbitcoin_derived_in_7_0()
        {
            var shapes = new[]
            {
                Abandon.ToUpperInvariant(), Abandon.Replace(" ", "  "), "  " + Abandon + "\n",
                Abandon + " abandon", "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon"
            };
            var failures = new List<string>();
            foreach (var shape in shapes)
            {
                Mnemonic expected;
                try { expected = new Mnemonic(shape); }
                catch (Exception) { continue; }
                var wallet = new Wallet(shape, "pw", Wallet.DEFAULT_PATH, null, false);
                if (wallet.Seed != expected.DeriveSeed("pw").ToHex()) failures.Add(shape);
            }
            Assert.True(failures.Count == 0, string.Join("|", failures));
        }

        [Trait("Rule", "S9-R2-51")]
        [Fact]
        public void Given_invalid_mnemonics_When_constructing_strictly_Then_each_is_rejected_with_ArgumentException()
        {
            var accepted = new List<string>();
            foreach (var shape in new[]
            {
                "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon",
                Abandon + " abandon", Abandon + " zoo", "zzzzz " + Abandon.Substring(8), "hello", "",
                "ábaco ábaco ábaco ábaco ábaco ábaco ábaco ábaco ábaco ábaco ábaco abierto"
            })
            {
                try { new Wallet(shape, null); accepted.Add(shape); }
                catch (ArgumentException) { }
                catch (Exception ex) { accepted.Add(shape + " => " + ex.GetType().Name); }
            }
            Assert.True(accepted.Count == 0, string.Join("|", accepted));
        }

        [Trait("Rule", "S9-R2-52")]
        [Fact]
        public void Given_a_valid_mnemonic_with_extra_whitespace_When_constructing_strictly_Then_the_seed_equals_the_canonical_seed()
        {
            var canonical = new Wallet(Abandon, null).Seed;
            foreach (var variant in new[] { Abandon + " ", Abandon.Replace(" ", "  "), Abandon + "\n" })
            {
                try { Assert.Equal(canonical, new Wallet(variant, null).Seed); }
                catch (ArgumentException) { }
            }
        }

        [Trait("Rule", "S9-R2-53")]
        [Trait("Supersededby", "S9-R5-A")]
        [Fact]
        public void Given_the_4096_character_boundary_When_constructing_Then_input_above_it_is_rejected_and_input_at_it_is_not_rejected_for_length()
        {
            var padded = Abandon + new string(' ', 4097 - Abandon.Length);
            Assert.Equal(new Mnemonic(padded).DeriveSeed(null).ToHex(), new Wallet(padded, null, Wallet.DEFAULT_PATH, null, false).Seed);
            Assert.NotNull(new Wallet(Abandon + new string(' ', 4096 - Abandon.Length), null, Wallet.DEFAULT_PATH, null, false).Seed);
            Assert.ThrowsAny<ArgumentException>(() => new Wallet(Abandon, new string('p', 4097)));
            Assert.NotNull(new Wallet(Abandon, new string('p', 4096)).Seed);
        }

        [Trait("Rule", "S9-R2-54")]
        [Fact]
        public void Given_a_caller_random_When_generating_a_wallet_Then_it_is_used_locally_only_and_the_global_random_is_untouched()
        {
            var original = RandomUtils.Random;
            var fixedRandom = new FixedRandom();
            var wallet = new Wallet(Wordlist.English, WordCount.Twelve, null, Wallet.DEFAULT_PATH, fixedRandom);
            Assert.True(fixedRandom.Calls > 0);
            Assert.Same(original, RandomUtils.Random);
            Assert.Equal(new Mnemonic(Wordlist.English, Enumerable.Repeat((byte)7, 16).ToArray()).ToString(), string.Join(" ", wallet.Words));
            foreach (var count in new[] { WordCount.Twelve, WordCount.Fifteen, WordCount.Eighteen, WordCount.TwentyOne, WordCount.TwentyFour })
            {
                var generated = new Wallet(Wordlist.English, count);
                Assert.Equal((int)count, generated.Words.Length);
                Assert.True(generated.IsMnemonicValidChecksum);
            }
            Assert.NotEqual(new Wallet(Wordlist.English, WordCount.Twelve).Seed, new Wallet(Wordlist.English, WordCount.Twelve).Seed);
        }
    }
}
