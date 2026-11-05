using System;
using Nethereum.PrivacyPools;
using Xunit;

namespace Nethereum.PrivacyPools.Tests
{
    public class SecurityAudit71MnemonicTests
    {
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        private const string BadChecksum =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon";

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_a_non_standard_mnemonic_When_creating_an_account_Then_the_default_rejects_it_and_the_opt_out_derives_keys()
        {
            Assert.Throws<ArgumentException>(() => new PrivacyPoolAccount(BadChecksum));
            Assert.Throws<ArgumentException>(() => PrivacyPoolAccount.CreateLegacy(BadChecksum));
            var lenient = new PrivacyPoolAccount(BadChecksum, "", false);
            var legacyLenient = PrivacyPoolAccount.CreateLegacy(BadChecksum, "", false);
            Assert.NotEqual(0, lenient.MasterNullifier);
            Assert.NotEqual(0, legacyLenient.MasterSecret);
        }

        [Trait("Rule", "S9-20")]
        [Fact]
        public void Given_a_valid_mnemonic_When_using_the_opt_out_overload_Then_the_keys_equal_the_default_constructor()
        {
            var strict = new PrivacyPoolAccount(Abandon);
            var lenient = new PrivacyPoolAccount(Abandon, "", false);
            Assert.Equal(strict.MasterNullifier, lenient.MasterNullifier);
            Assert.Equal(strict.MasterSecret, lenient.MasterSecret);
        }
    }
}
