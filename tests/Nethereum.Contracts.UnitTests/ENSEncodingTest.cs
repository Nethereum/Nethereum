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

    }
}