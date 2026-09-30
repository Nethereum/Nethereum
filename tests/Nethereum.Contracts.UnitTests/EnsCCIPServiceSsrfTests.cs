using Nethereum.Contracts.Standards.ENS;
using Xunit;

namespace Nethereum.Contracts.UnitTests
{
    /// <summary>
    /// SSRF (CWE-918) hardening of the EIP-3668 CCIP-Read gateway URL handling. The gateway URL is
    /// supplied by an untrusted resolver contract, so by default only https is allowed and hosts that
    /// resolve to private/reserved/link-local/loopback destinations are refused. IP-literal URLs are
    /// used so validation performs no real DNS network lookup.
    /// </summary>
    public class EnsCCIPServiceSsrfTests
    {
        private class TestableEnsCCIPService : EnsCCIPService
        {
            public void Validate(string url) => ValidateCcipUrl(url);
        }

        [Theory]
        [InlineData("http://example.com/gateway/{sender}/{data}.json")]
        [InlineData("https://127.0.0.1/")]
        [InlineData("https://169.254.169.254/latest/meta-data/")]
        [InlineData("https://10.0.0.5/")]
        [InlineData("https://192.168.1.10/")]
        [InlineData("https://172.16.5.4/")]
        [InlineData("https://100.64.0.1/")]
        [InlineData("https://[::1]/")]
        [InlineData("https://[::]/")]
        public void ShouldRefuseUnsafeCcipGatewayUrls(string url)
        {
            var ccipService = new TestableEnsCCIPService();
            Assert.Throws<EnsCCIPService.CCIPReadUrlValidationException>(() => ccipService.Validate(url));
        }

        [Fact]
        public void ShouldAllowPublicHttpsCcipGatewayUrl()
        {
            var ccipService = new TestableEnsCCIPService();
            ccipService.Validate("https://8.8.8.8/gateway/{sender}/{data}.json");
        }

        [Fact]
        public void ShouldAllowHttpWhenExplicitlyOptedIn()
        {
            var ccipService = new TestableEnsCCIPService { AllowHttp = true };
            ccipService.Validate("http://8.8.8.8/gateway");
        }

        [Fact]
        public void ShouldAllowPrivateDestinationsWhenExplicitlyOptedIn()
        {
            var ccipService = new TestableEnsCCIPService { AllowPrivateDestinations = true };
            ccipService.Validate("https://127.0.0.1/gateway");
        }
    }
}
