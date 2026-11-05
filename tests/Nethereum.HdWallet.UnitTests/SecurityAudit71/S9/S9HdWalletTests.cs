using System;
using System.Linq;
using NBitcoin;
using Xunit;
using KeyPath = NBitcoin.KeyPath;

namespace Nethereum.HdWallet.UnitTests.SecurityAudit71.S9
{
    public class S9HdWalletTests
    {
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        private const string BadChecksum =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon";
        private const string NotInWordList =
            "zzzzz abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        [Trait("Rule", "S9-21")]
        [Fact]
        public void Given_a_mnemonic_with_a_bad_checksum_When_creating_an_HdWallet_Wallet_Then_it_is_rejected()
        {
            Assert.ThrowsAny<Exception>(() => new Wallet(BadChecksum, null));
        }

        [Trait("Rule", "S9-27")]
        [Fact]
        public void Given_a_negative_index_When_getting_a_key_from_the_HdWallet_Wallet_Then_it_is_rejected()
        {
            var wallet = new Wallet(Abandon, "");
            Assert.ThrowsAny<Exception>(() => wallet.GetPrivateKey(-1));
        }

        [Trait("Rule", "S9-28")]
        [Fact]
        public void Given_a_caller_supplied_random_When_creating_an_HdWallet_Wallet_Then_the_process_wide_NBitcoin_random_is_not_replaced()
        {
            var original = RandomUtils.Random;
            try
            {
                var constant = new ConstantRandom();
                new Wallet(Abandon, "", Wallet.DEFAULT_PATH, constant);
                Assert.False(ReferenceEquals(RandomUtils.Random, constant),
                    "NBitcoin RandomUtils.Random is now the caller supplied instance for the whole process");
            }
            finally
            {
                RandomUtils.Random = original;
            }
        }

        [Trait("Rule", "S9-23")]
        [Fact]
        public void Given_a_composed_and_decomposed_passphrase_When_creating_an_HdWallet_Wallet_Then_the_seed_is_the_same()
        {
            var composed = "caféÅΩ";
            var decomposed = composed.Normalize(System.Text.NormalizationForm.FormKD);
            Assert.Equal(new Wallet(Abandon, composed).Seed, new Wallet(Abandon, decomposed).Seed);
        }

        [Trait("Rule", "S9-29")]
        [Fact]
        public void Given_two_generated_wallets_When_comparing_Then_they_differ_and_have_valid_checksums()
        {
            var wa = new Wallet(Wordlist.English, WordCount.Twelve);
            var wb = new Wallet(Wordlist.English, WordCount.Twelve);
            Assert.NotEqual(string.Join(" ", wa.Words), string.Join(" ", wb.Words));
            Assert.True(wa.IsMnemonicValidChecksum);
        }

        [Trait("Rule", "S9-26")]
        [Fact]
        public void Given_the_abandon_about_mnemonic_When_deriving_the_default_ethereum_path_Then_the_address_is_the_published_one()
        {
            Assert.Equal("0x9858EfFD232B4033E47d90003D41EC34EcaEda94", new Wallet(Abandon, "").GetAddresses(1)[0]);
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
