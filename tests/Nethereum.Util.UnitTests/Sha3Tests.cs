using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class Sha3Tests
    {
        [Fact]
        public void ShouldCalculateHashFromHexForMultipleStrings()
        {
            var keccak = new Sha3Keccack();
            var result = keccak.CalculateHashFromHex(
                "0x93cdeb708b7545dc668eb9280176169d1c33cfd8ed6f04690a0bcc88a93fc4ae",
                "0x0d57c7d8b54ebf963f94cded8a57a1b109ac7465ada218575473648bf373b90d");

            Assert.Equal("13265b3c8b785f6715b215cb1e6869312588a03afe0076beda8042c2ceb5603b", result);
        }
    }
}