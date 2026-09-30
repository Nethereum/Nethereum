using System;
using Nethereum.AccountAbstraction.AppChain.Services;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.Services
{
    public class QuorumRoleSpecTests
    {
        private const string ValidatorAddress = "0x1111111111111111111111111111111111111a";
        private const string TargetAddress = "0x2222222222222222222222222222222222222b";
        private const string PolicyAddress = "0x3333333333333333333333333333333333333c";

        private static byte[] Selector() => new byte[] { 0x01, 0x02, 0x03, 0x04 };
        private static byte[] RuleId() => new byte[32];
        private static byte[] Salt() => new byte[32];

        [Fact]
        [Trait("Category", "Unit-AppChainModernization")]
        public void Given_owner_addresses_with_an_exact_duplicate_When_constructing_a_QuorumRoleSpec_Then_it_throws()
        {
            var owners = new[]
            {
                "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            };

            var ex = Assert.Throws<ArgumentException>(() => new QuorumRoleSpec(
                validatorAddress: ValidatorAddress,
                ownerAddresses: owners,
                threshold: 2,
                targetAddress: TargetAddress,
                functionSelector: Selector(),
                policyAddress: PolicyAddress,
                policyRuleId: RuleId(),
                cap: 100,
                salt: Salt()));

            Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Category", "Unit-AppChainModernization")]
        public void Given_owner_addresses_that_are_the_same_case_insensitively_When_constructing_a_QuorumRoleSpec_Then_it_throws()
        {
            var owners = new[]
            {
                "0xAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
            };

            Assert.Throws<ArgumentException>(() => new QuorumRoleSpec(
                validatorAddress: ValidatorAddress,
                ownerAddresses: owners,
                threshold: 2,
                targetAddress: TargetAddress,
                functionSelector: Selector(),
                policyAddress: PolicyAddress,
                policyRuleId: RuleId(),
                cap: 100,
                salt: Salt()));
        }

        [Fact]
        [Trait("Category", "Unit-AppChainModernization")]
        public void Given_distinct_owner_addresses_When_constructing_a_QuorumRoleSpec_Then_it_succeeds()
        {
            var owners = new[]
            {
                "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "0xcccccccccccccccccccccccccccccccccccccc"
            };

            var spec = new QuorumRoleSpec(
                validatorAddress: ValidatorAddress,
                ownerAddresses: owners,
                threshold: 2,
                targetAddress: TargetAddress,
                functionSelector: Selector(),
                policyAddress: PolicyAddress,
                policyRuleId: RuleId(),
                cap: 100,
                salt: Salt());

            Assert.Equal(owners, spec.OwnerAddresses);
        }
    }
}
