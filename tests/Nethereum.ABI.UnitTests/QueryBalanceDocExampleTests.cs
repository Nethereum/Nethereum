using System.Numerics;
using Nethereum.Util;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class QueryBalanceDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "query-balance", "Convert Wei balance to Ether")]
        public void ShouldConvertWeiBalanceToEther()
        {
            var balanceInWei = BigInteger.Parse("1110000000000000000");
            var balanceInEther = UnitConversion.Convert.FromWei(balanceInWei);

            Assert.Equal(1.11m, balanceInEther);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "query-balance", "Convert Ether to Wei round-trip")]
        public void ShouldConvertEtherToWeiRoundTrip()
        {
            var balanceInWei = BigInteger.Parse("1110000000000000000");
            var balanceInEther = UnitConversion.Convert.FromWei(balanceInWei);
            var backToWei = UnitConversion.Convert.ToWei(balanceInEther);

            Assert.Equal(balanceInWei, backToWei);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "query-balance", "ERC20 token balance conversion with 18 decimals")]
        public void ShouldConvertErc20BalanceWith18Decimals()
        {
            var rawBalance = BigInteger.Parse("1000000000000000000");
            var humanBalance = UnitConversion.Convert.FromWei(rawBalance, 18);

            Assert.Equal(1m, humanBalance);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "query-balance", "ERC20 token balance conversion with 6 decimals (USDC)")]
        public void ShouldConvertErc20BalanceWith6Decimals()
        {
            var rawBalance = BigInteger.Parse("1000000");
            var humanBalance = UnitConversion.Convert.FromWei(rawBalance, 6);

            Assert.Equal(1m, humanBalance);
        }
    }
}
