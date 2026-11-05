using System;
using Nethereum.Siwe.Core;
using Xunit;

namespace Nethereum.Siwe.UnitTests
{
    public class SiweTrailingNewlineTests
    {
        private const string Base = "service.org wants you to sign in with your Ethereum account:\n0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2\n\nI accept the ServiceOrg Terms of Service: https://service.org/tos\n\nURI: https://service.org/login\nVersion: 1\nChain ID: 1\nNonce: 32891757\nIssued At: 2021-09-30T16:25:24.000Z";
        private const string WithResources = Base + "\nResources:\n- ipfs://Qme7ss3ARVgxv6rXqVPiikMJ8u2NLgmgszg13pYrDKEoiu\n- https://example.com/my-web2-claim.json";
        private const string WithRequestId = Base + "\nRequest ID: abc";

        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        [InlineData("\n\n")]
        [InlineData("\r\n\r\n")]
        [InlineData("\n\r\n\n")]
        public void Given_OnlyEmptyLinesAfterTheLastField_When_Parsed_Then_TheResultMatchesTheMessageWithoutThem(string trailing)
        {
            foreach (var message in new[] { Base, WithResources, WithRequestId })
            {
                var expected = SiweMessageParser.Parse(message);
                var parsed = SiweMessageParser.Parse(message + trailing);

                Assert.Equal(SiweMessageStringBuilder.BuildMessage(expected), SiweMessageStringBuilder.BuildMessage(parsed));
                Assert.Equal(expected.IssuedAt, parsed.IssuedAt);
                Assert.Equal(expected.RequestId, parsed.RequestId);
                Assert.Equal(expected.Resources?.Count, parsed.Resources?.Count);
            }
        }

        [Theory]
        [InlineData("\nBAD")]
        [InlineData("\n \n")]
        [InlineData("\r")]
        [InlineData("\r\r\n")]
        [InlineData("\n\nBAD\n")]
        [InlineData(" \n")]
        public void Given_NonEmptyContentAfterTheLastField_When_Parsed_Then_ItIsRejected(string trailing)
        {
            Assert.Throws<ArgumentException>(() => SiweMessageParser.Parse(Base + trailing));
            Assert.Throws<ArgumentException>(() => SiweMessageParser.Parse(WithRequestId + trailing));
            Assert.Throws<ArgumentException>(() => SiweMessageParser.Parse(WithResources + "\nBAD" + trailing));
        }

        [Fact]
        public void Given_OnlyNewlinesAfterTheMessageHeaderOnly_When_Parsed_Then_ItIsRejected()
        {
            Assert.Throws<ArgumentException>(() => SiweMessageParser.Parse("\n\n"));
        }
    }
}
