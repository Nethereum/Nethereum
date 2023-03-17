using Nethereum.Contracts.Standards.ENS;
using Xunit;

namespace Nethereum.Contracts.UnitTests
{
    public class ENSEncodingTest
    {
        /*
         * 
        namehash('') = 0x0000000000000000000000000000000000000000000000000000000000000000
        namehash('eth') = 0x93cdeb708b7545dc668eb9280176169d1c33cfd8ed6f04690a0bcc88a93fc4ae
        namehash('foo.eth') = 0xde9b09fd7c5f901e23a3f19fecc54828e9c848539801e86591bd9801b019f84f
        */
        [Fact]
        public void ShouldEncodeCorrectly()
        {
            var ensUtil = new EnsUtil();
            //empty
            Assert.Equal("0x0000000000000000000000000000000000000000000000000000000000000000", ensUtil.GetNameHash(""));
            //tld
            Assert.Equal("0x93cdeb708b7545dc668eb9280176169d1c33cfd8ed6f04690a0bcc88a93fc4ae", ensUtil.GetNameHash("eth"));
            //foo.eth
            Assert.Equal("0xde9b09fd7c5f901e23a3f19fecc54828e9c848539801e86591bd9801b019f84f", ensUtil.GetNameHash("foo.eth"));
            //normalise ascii domain
            Assert.Equal("foo.eth", ensUtil.Normalise("foo.eth"));
        }

        [Fact]
        public async void ShouldNormaliseAsciiDomain()
        {
            var input = "foo.eth"; // latin chars only
            var expected = "foo.eth";
            var output = new EnsUtil().Normalise(input);
            Assert.Equal(expected, output);
        }


        [Fact]
        public void ShouldNormaliseInternationalDomain()
        {
            var input = "fоо.eth"; // with cyrillic 'o'
            var expected = "fоо.eth";
            var output = new EnsUtil().Normalise(input);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void ShouldNormaliseToLowerDomain()
        {
            var input = "Foo.eth";
            var expected = "foo.eth";
            var output = new EnsUtil().Normalise(input);
            Assert.Equal(expected, output);
        }

        [Fact]
        public void ShouldNormaliseEmojiDomain()
        {
            var input = "🦚.eth";
            var expected = "🦚.eth";
            var output = new EnsUtil().Normalise(input);
            Assert.Equal(expected, output);
        }


        [Theory]
        [InlineData("vitalik.eth", "0x07766974616c696b0365746800")]
        [InlineData("vitalik.wallet.eth", "0x07766974616c696b0677616c6c65740365746800")]
        [InlineData("ViTalIk.WALlet.Eth", "0x07766974616c696b0677616c6c65740365746800")]
        [InlineData("123.eth", "0x033132330365746800")]
        [InlineData("öbb.at", "0x04c3b6626202617400")]
        [InlineData("Ⓜ", "0x016d00")]
        [InlineData("💩💩︎💩️", "0x0cf09f92a9f09f92a9f09f92a900")]
        public void ShouldDnsEncode(string domain, string expected)
        {
            var encoded = new EnsUtil().DnsEncode(domain);
            Assert.Equal(expected, encoded);
        }

    }
}