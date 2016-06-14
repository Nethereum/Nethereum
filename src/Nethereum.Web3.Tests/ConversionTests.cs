using System.Numerics;
using Nethereum.Web3;
using Xunit;

namespace SimpleTests
{
   
    public class ConversionTests
    {
        [Fact]
        public void ShouldConvertFromWeiAndBackToEth()
        {
            var unitConversion = new UnitConversion();
            var val = BigInteger.Parse("1000000000000000000000000001");
            var result = unitConversion.FromWei(val, 18);
            var result2 = unitConversion.FromWei(val);
            Assert.Equal(result, result2);
            result2 = unitConversion.FromWei(val, BigInteger.Parse("1000000000000000000"));
            Assert.Equal(result, result2);
            Assert.Equal(val, UnitConversion.Convert.ToWei(result));

        }
    }
}