using Xunit;

namespace Nethereum.ENS.UnitTests
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
            Assert.Equal("0x0000000000000000000000000000000000000000000000000000000000000000", ensUtil.GetHash(""));
            //tld
            Assert.Equal("0x93cdeb708b7545dc668eb9280176169d1c33cfd8ed6f04690a0bcc88a93fc4ae", ensUtil.GetHash("eth"));
            //foo.eth
            Assert.Equal("0xde9b09fd7c5f901e23a3f19fecc54828e9c848539801e86591bd9801b019f84f", ensUtil.GetHash("foo.eth"));
            //normalise ascii domain
            Assert.Equal("foo.eth", ensUtil.Normalise("foo.eth"));
            //normalise international domain
            //with crylic 'o' 
            Assert.Equal("xn--f-1tba.eth", ensUtil.Normalise("fоо.eth"));

        }
    }
}