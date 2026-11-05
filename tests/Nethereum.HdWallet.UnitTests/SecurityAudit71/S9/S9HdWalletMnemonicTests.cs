using System;
using System.Linq;
using NBitcoin;
using Xunit;

namespace Nethereum.HdWallet.UnitTests.SecurityAudit71.S9
{
    public class S9HdWalletMnemonicTests
    {
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        private const string BadChecksum =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon";
        private const string TwentyFour =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art";

        [Trait("Rule", "S9-21")]
        [Fact]
        public void Given_invalid_mnemonics_When_creating_a_Wallet_Then_an_ArgumentException_is_thrown()
        {
            Assert.Throws<ArgumentException>(() => new Wallet(BadChecksum, ""));
            Assert.Throws<ArgumentException>(() => new Wallet("abandon abandon abandon", ""));
            Assert.Throws<ArgumentException>(() => new Wallet("zzzzz " + string.Join(" ", Enumerable.Repeat("abandon", 10)) + " about", ""));
        }

        [Trait("Rule", "S9-21")]
        [Fact]
        public void Given_the_published_24_word_vector_When_creating_a_Wallet_Then_it_is_accepted_and_reports_a_valid_checksum()
        {
            var wallet = new Wallet(TwentyFour, "");
            Assert.True(wallet.IsMnemonicValidChecksum);
            Assert.Equal(24, wallet.Words.Length);
        }

        [Trait("Rule", "S9-21")]
        [Fact]
        public void Given_validation_is_turned_off_When_creating_a_Wallet_from_a_bad_checksum_Then_it_is_created_and_reports_the_invalid_checksum()
        {
            var wallet = new Wallet(BadChecksum, "", Wallet.DEFAULT_PATH, null, false);
            Assert.False(wallet.IsMnemonicValidChecksum);
            Assert.NotNull(wallet.GetPrivateKey(0));
        }

        [Trait("Rule", "S9-22")]
        [Fact]
        public void Given_a_mnemonic_or_passphrase_longer_than_4096_characters_When_creating_a_Wallet_Then_an_ArgumentException_is_thrown()
        {
            Assert.Throws<ArgumentException>(() => new Wallet(new string('a', 4097), ""));
            Assert.Throws<ArgumentException>(() => new Wallet(Abandon, new string('a', 4097)));
        }

        [Trait("Rule", "S9-28")]
        [Fact]
        public void Given_a_constant_random_When_generating_a_Wallet_Then_the_words_come_from_that_random_for_that_wallet_only()
        {
            var original = RandomUtils.Random;
            var first = new Wallet(Wordlist.English, WordCount.Twelve, null, Wallet.DEFAULT_PATH, new ConstantRandom());
            var second = new Wallet(Wordlist.English, WordCount.Twelve, null, Wallet.DEFAULT_PATH, new ConstantRandom());
            Assert.Equal(first.Words, second.Words);
            Assert.Equal(new Mnemonic(Wordlist.English, Enumerable.Repeat((byte)7, 16).ToArray()).Words, first.Words);
            Assert.Same(original, RandomUtils.Random);
        }

        private class ConstantRandom : IRandom
        {
            public void GetBytes(byte[] output)
            {
                for (var i = 0; i < output.Length; i++) output[i] = 7;
            }

            public void GetBytes(Span<byte> output)
            {
                output.Fill(7);
            }
        }
    }
}
