using Nethereum.ABI.Util;
using Nethereum.ENS;
using Xunit;

namespace Nethereum.Web3.Tests
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
            Assert.Equal("0x0000000000000000000000000000000000000000000000000000000000000000", ensUtil.GetEnsNameHash(""));
            Assert.Equal("0x93cdeb708b7545dc668eb9280176169d1c33cfd8ed6f04690a0bcc88a93fc4ae", ensUtil.GetEnsNameHash("eth"));
            Assert.Equal("0xde9b09fd7c5f901e23a3f19fecc54828e9c848539801e86591bd9801b019f84f", ensUtil.GetEnsNameHash("foo.eth"));
        }
    }
}