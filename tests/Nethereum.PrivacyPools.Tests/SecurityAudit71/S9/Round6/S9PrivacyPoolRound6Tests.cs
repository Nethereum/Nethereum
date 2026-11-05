using System;
using Nethereum.PrivacyPools;
using Xunit;

namespace Nethereum.PrivacyPools.Tests.SecurityAudit71.S9.Round6
{
    public class S9PrivacyPoolRound6Tests
    {
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        [Trait("Rule", "S9-R6-5")]
        [Fact]
        public void Given_over_long_and_non_standard_phrases_When_creating_a_privacy_pool_account_Then_lenient_accepts_and_strict_rejects()
        {
            var phrase = Abandon + new string(' ', 5000);
            var lenient = new PrivacyPoolAccount(phrase, "p", false);
            var legacy = PrivacyPoolAccount.CreateLegacy(phrase, "p", false);
            Assert.NotEqual(lenient.MasterNullifier, new PrivacyPoolAccount(Abandon, "p").MasterNullifier);
            Assert.NotNull(legacy);
            Assert.ThrowsAny<ArgumentException>(() => new PrivacyPoolAccount(phrase, "p"));
            Assert.ThrowsAny<ArgumentException>(() => new PrivacyPoolAccount(phrase, "p", true));
            Assert.NotNull(new PrivacyPoolAccount(Abandon.ToUpperInvariant(), "p", false));
        }
    }
}
