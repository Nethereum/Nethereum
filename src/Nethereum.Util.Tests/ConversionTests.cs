using System;
using System.Numerics;
using Nethereum.Util;
using Xunit;
using System.Linq;
namespace Nethereum.Util.Tests
{

    public class ConversionTests
    {
        [Theory]
        [InlineData(18, "1111111111.111111111111111111",  "1111111111111111111111111111")]
        [InlineData(18, "11111111111.111111111111111111", "11111111111111111111111111111")]
        //Rounding happens when having more than 29 digits
        [InlineData(18, "111111111111.11111111111111111", "111111111111111111111111111111")]
        [InlineData(18, "1111111111111.1111111111111111", "1111111111111111111111111111111")]
        public void ShouldConvertFromWeiToDecimal(int units, string expected, string weiAmount)
        {
            var unitConversion = new UnitConversion();
            var result = unitConversion.FromWei(BigInteger.Parse(weiAmount), units);
            Assert.Equal(expected, result.ToString());
        }

        [Theory]
        [InlineData(18, "1111111111.111111111111111111", "1111111111111111111111111111")]
        [InlineData(18, "11111111111.111111111111111111", "11111111111111111111111111111")]
        [InlineData(18, "111111111111.111111111111111111", "111111111111111111111111111111")]
        [InlineData(18, "1111111111111.111111111111111111", "1111111111111111111111111111111")]
        [InlineData(30, "1111111111111111111.111111111111111111111111111111", "1111111111111111111111111111111111111111111111111")]
        public void ShouldConvertFromWeiToBigDecimal(int units, string expected, string weiAmount)
        {
            var unitConversion = new UnitConversion();
            var result = unitConversion.FromWeiToBigDecimal(BigInteger.Parse(weiAmount), units);
            Assert.Equal(expected, result.ToString());
        }

        [Fact]
        public void ShouldConvertToWeiUsingNumberOfDecimalsEth()
        {
            var unitConversion = new UnitConversion();
            var val = BigInteger.Parse("1000000000000000000000000001");
            var result = unitConversion.FromWei(val, 18);
            var result2 = unitConversion.FromWei(val);
            Assert.Equal(result, result2);
            result2 = unitConversion.FromWei(val, BigInteger.Parse("1000000000000000000"));
            Assert.Equal(result, result2);
            Assert.Equal(val, UnitConversion.Convert.ToWei(result, 18));
        }

        [Fact]
        public void ShouldNotFailToConvertUsing0DecimalUnits()
        {
            var unitConversion = new UnitConversion();
            var val = BigInteger.Parse("1000000000000000000000000001");
            var result = unitConversion.FromWei(val, 18);
            Assert.Equal(1000000000, UnitConversion.Convert.ToWei(result, (int)0));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void ShouldFailToConvertUsingNonPowerOf10Units(int value)
        {
            var unitConversion = new UnitConversion();
            var val = BigInteger.Parse("1000000000000000000000000001");
            var result = unitConversion.FromWei(val, 18);
            Assert.Throws<Exception>(() => UnitConversion.Convert.ToWei(result, new BigInteger(value)));
        }

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
        public void ShouldConverFromWei()
        {
            var unitConversion = new UnitConversion();
            var unit = BigInteger.Parse("100000000000000000000000000");
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
        public void TrimmingOf0sShouldOnlyHappenForDecimalValues()
        {
            var unitConversion = new UnitConversion();
            var result1 = unitConversion.ToWei(10m);
            var result2 = unitConversion.ToWei(100m);
            Assert.NotEqual(result1.ToString(), result2.ToString());
        }

        [Fact]
        public void ShouldConvertNoDecimalIn10s()
        {
            var unitConversion = new UnitConversion();
            var ether = 10m;
            var wei = UnitConversion.Convert.ToWei(ether, UnitConversion.EthUnit.Ether);
            var val = BigInteger.Parse("1".PadRight(20, '0'));
            var result = unitConversion.FromWei(val, 18);
            Assert.Equal(UnitConversion.Convert.ToWei(result), wei);
        }


        [Fact]
        public void ShouldConvertFromDecimalUnit()
        {
            var unitConversion = new UnitConversion();
            var ether = 0.0010m;
            var wei = UnitConversion.Convert.ToWei(ether, UnitConversion.EthUnit.Ether);
            var val = BigInteger.Parse("1".PadRight(16, '0'));
            var result = unitConversion.FromWei(val, 18);
            Assert.Equal(UnitConversion.Convert.ToWei(result), wei);
        }

        [Fact]
        public void ShouldConvertPeriodicGether()
        {
            var unitConversion = new UnitConversion();
            var ether = (decimal)1 / 3;
            var wei = UnitConversion.Convert.ToWei(ether, UnitConversion.EthUnit.Gether);
            var val = BigInteger.Parse("3".PadLeft(27, '3'));
            var result = unitConversion.FromWei(val, UnitConversion.EthUnit.Gether);
            Assert.Equal(UnitConversion.Convert.ToWei(result, UnitConversion.EthUnit.Gether), wei);
        }

        [Fact]
        public void ShouldConvertPeriodicTether()
        {
            var unitConversion = new UnitConversion();
            var ether = new BigDecimal(1)  / new BigDecimal(3);
            var wei = UnitConversion.Convert.ToWei(ether, UnitConversion.EthUnit.Tether);
            var val = BigInteger.Parse("3".PadLeft(30, '3'));
            var result = unitConversion.FromWeiToBigDecimal(val, UnitConversion.EthUnit.Tether);
            Assert.Equal(UnitConversion.Convert.ToWei(result, UnitConversion.EthUnit.Tether), wei);
        }
    }
}