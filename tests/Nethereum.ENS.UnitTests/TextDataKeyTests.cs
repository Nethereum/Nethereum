using Nethereum.Contracts.Standards.ENS;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.ENS.UnitTests
{
    public class TextDataKeyTests
    {
        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "ens-contract-services", "Text record key as the ENSIP-5 string", Order = 1)]
        public void ShouldConvertUnderscoresToDotsInTheTextRecordKey()
        {
            Assert.Equal("url", Nethereum.ENS.TextDataKey.url.GetDataKeyAsString());
            Assert.Equal("com.twitter", Nethereum.ENS.TextDataKey.com_twitter.GetDataKeyAsString());
            Assert.Equal("eth.ens.delegate", Nethereum.ENS.TextDataKey.eth_ens_delegate.GetDataKeyAsString());
        }

        [Fact]
        [NethereumDocExample(DocSection.SmartContracts, "ens-contract-services", "Name hash and label hash", Order = 2)]
        public void ShouldCalculateTheNameHashAndLabelHash()
        {
            var ensUtil = new EnsUtil();

            Assert.Equal("0x0000000000000000000000000000000000000000000000000000000000000000", ensUtil.GetNameHash(""));
            Assert.Equal("0x93cdeb708b7545dc668eb9280176169d1c33cfd8ed6f04690a0bcc88a93fc4ae", ensUtil.GetNameHash("eth"));
            Assert.Equal("0xde9b09fd7c5f901e23a3f19fecc54828e9c848539801e86591bd9801b019f84f", ensUtil.GetNameHash("foo.eth"));
        }
    }
}
