using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nethereum.Web3;

namespace SimpleTests
{
    [TestClass]
    public class ConversionTests
    {
        [TestMethod]
        public void Test()
        {
            var unitConversion = new UnitConversion();
            var val = BigInteger.Parse("1000000000000000000000000001");
            var result = unitConversion.FromWei(val, 18);
            var result2 = unitConversion.FromWei(val);
            Assert.AreEqual(result, result2);
            result2 = unitConversion.FromWei(val, BigInteger.Parse("1000000000000000000"));
            Assert.AreEqual(result, result2);
            Assert.AreEqual(val, UnitConversion.Convert.ToWei(result));

        }
    }
}