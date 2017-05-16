using System.Numerics;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Util.Tests
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

        [Fact]
        public void ShouldConvertPeriodic()
        {
            var unitConversion = new UnitConversion();
            var ether = (decimal)1 / 3;
            var wei = UnitConversion.Convert.ToWei(ether, UnitConversion.EthUnit.Ether);
            var val = BigInteger.Parse("333333333333333333");
            var result = unitConversion.FromWei(val, 18);
            Assert.Equal(UnitConversion.Convert.ToWei(result), wei);
        }

        [Fact]
        public void ShouldConvertLargeDecimal()
        {
            var unitConversion = new UnitConversion();
            var ether = 1.243842387924387924897423897423m;
            var wei = UnitConversion.Convert.ToWei(ether, UnitConversion.EthUnit.Ether);
            var val = BigInteger.Parse("1243842387924387924");
            var result = unitConversion.FromWei(val, 18);
            Assert.Equal(UnitConversion.Convert.ToWei(result), wei);
        }


        [Fact]
        public void ShouldConvertSmallDecimal()
        {
            var unitConversion = new UnitConversion();
            var ether = 1.24384m;
            var wei = UnitConversion.Convert.ToWei(ether, UnitConversion.EthUnit.Ether);
            var val = BigInteger.Parse("124384".PadRight(19,'0'));
            var result = unitConversion.FromWei(val, 18);
            Assert.Equal(UnitConversion.Convert.ToWei(result), wei);
        }

        [Fact]
        public void ShouldConvertNoDecimal()
        {
            var unitConversion = new UnitConversion();
            var ether = 1m;
            var wei = UnitConversion.Convert.ToWei(ether, UnitConversion.EthUnit.Ether);
            var val = BigInteger.Parse("1".PadRight(19, '0'));
            var result = unitConversion.FromWei(val, 18);
            Assert.Equal(UnitConversion.Convert.ToWei(result), wei);
        }

        [Fact]
        public void ShouldConvertPeriodicTether()
        {
            var unitConversion = new UnitConversion();
            var ether = (decimal)1 / 3;
            var wei = UnitConversion.Convert.ToWei(ether, UnitConversion.EthUnit.Gether);
            var val = BigInteger.Parse("3".PadLeft(27, '3'));
            var result = unitConversion.FromWei(val, UnitConversion.EthUnit.Gether);
            Assert.Equal(UnitConversion.Convert.ToWei(result, UnitConversion.EthUnit.Gether), wei);
        }
    }
}