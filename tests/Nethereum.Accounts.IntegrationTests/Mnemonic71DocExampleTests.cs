using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Accounts.Bip32;
using Nethereum.Documentation;
using Nethereum.Util.Rest;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    public class Mnemonic71DocExampleTests
    {
        private const string Mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "account-types", "Validate a BIP-39 mnemonic and derive keys")]
        public void ShouldValidateMnemonicAndDeriveKeys()
        {
            Assert.True(Bip39.IsValidMnemonic(Mnemonic));
            Bip39.ValidateMnemonic(Mnemonic);
            Assert.Equal(Mnemonic, Bip39.CanonicalMnemonic("  ABANDON abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about "));

            var wallet = new MinimalHDWallet(Mnemonic);
            Assert.Equal("0x9858EfFD232B4033E47d90003D41EC34EcaEda94", wallet.GetEthereumAddress(0));
            Assert.Equal(wallet.GetEthereumKey(0).GetPublicAddress(), wallet.GetKeyFromPath("m/44'/60'/0'/0/0").GetPublicAddress());
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "account-types", "Reject an invalid mnemonic, an oversized input and a hostile derivation path")]
        public void ShouldRejectInvalidMnemonicsAndPaths()
        {
            var badChecksum = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon";
            Assert.False(Bip39.IsValidMnemonic(badChecksum));
            Assert.Throws<ArgumentException>(() => new MinimalHDWallet(badChecksum));
            Assert.Throws<ArgumentException>(() => Bip39.NormalizeMnemonic(new string('a', Bip39.MaxInputLength + 1)));

            var wallet = new MinimalHDWallet(Mnemonic);
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m/" + new string('1', 11)));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m/01"));
            Assert.Throws<ArgumentException>(() => wallet.GetKeyFromPath("m" + string.Concat(System.Linq.Enumerable.Repeat("/0", MinimalHDWallet.MaxDepth + 1))));
            Assert.NotNull(wallet.GetKeyFromPath("m" + string.Concat(System.Linq.Enumerable.Repeat("/0", 3))));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "account-types", "Derive from a non-standard mnemonic without validation")]
        public void ShouldDeriveFromNonStandardMnemonicWhenValidationIsDisabled()
        {
            var nonStandard = "not a bip39 phrase at all";
            Assert.Throws<ArgumentException>(() => new MinimalHDWallet(nonStandard));
            var wallet = new MinimalHDWallet(nonStandard, "", false);
            Assert.NotNull(wallet.GetEthereumAddress(0));
            Assert.True(Bip39.IsNonStandardMnemonicForm("Abandon  abandon"));
            Assert.True(Bip39.IsLegacyUppercaseWordMnemonic("TRUE FALSE abandon"));
        }

        private class FixedBodyHandler : HttpMessageHandler
        {
            private readonly Func<HttpContent> _content;
            public FixedBodyHandler(Func<HttpContent> content) { _content = content; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = _content() });
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-rest", "Cap the size of a REST response")]
        public async Task ShouldRefuseAResponseOverTheCap()
        {
            var small = new RestHttpHelper(new HttpClient(new FixedBodyHandler(() => new StringContent("{\"a\":1}", Encoding.UTF8, "application/json"))));
            Assert.Equal(32L * 1024 * 1024, RestHttpHelper.DefaultMaxResponseBytes);
            Assert.Equal(RestHttpHelper.DefaultMaxResponseBytes, small.MaxResponseBytes);
            Assert.NotNull(await small.GetAsync<System.Collections.Generic.Dictionary<string, int>>("http://x.invalid/a"));

            var big = new RestHttpHelper(new HttpClient(new FixedBodyHandler(() => new StringContent("{\"a\":\"" + new string('x', 2000) + "\"}", Encoding.UTF8, "application/json"))))
            {
                MaxResponseBytes = 1024
            };
            await Assert.ThrowsAnyAsync<HttpRequestException>(() => big.GetAsync<System.Collections.Generic.Dictionary<string, string>>("http://x.invalid/b"));
        }
    }
}
