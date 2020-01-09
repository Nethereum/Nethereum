using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Org.BouncyCastle.Crypto.Digests;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class ABIPackingTests
    {

        [Fact]
        public virtual void ShouldPackBool()
        {
            var soliditySha3 = new ABIEncode();
            Assert.Equal("01", soliditySha3.GetABIEncodedPacked(true).ToHex());
            Assert.Equal("00", soliditySha3.GetABIEncodedPacked(false).ToHex());
        }

        [Fact]
        public virtual void ShouldPackDynamicArrayAddresses()
        {
            var list = new List<string>(new string[] { "0x7Dd31bc2ffA37Ab492a8d60F9C7170B78f12E1c5", "0x0Efa8015FCEC7039Feb656a4830Aa6518BF46010" });
            var soliditySha3 = new ABIEncode();
            Assert.Equal("0000000000000000000000007dd31bc2ffa37ab492a8d60f9c7170b78f12e1c50000000000000000000000000efa8015fcec7039feb656a4830aa6518bf46010", soliditySha3.GetABIEncodedPacked(new ABIValue("address[]", list)).ToHex());
        }

        [Fact]
        public virtual void ShouldPackStaticArrayAddresses()
        {
            var list = new List<string>(new string[] { "0x7Dd31bc2ffA37Ab492a8d60F9C7170B78f12E1c5", "0x0Efa8015FCEC7039Feb656a4830Aa6518BF46010" });
            var soliditySha3 = new ABIEncode();
            Assert.Equal("0000000000000000000000007dd31bc2ffa37ab492a8d60f9c7170b78f12e1c50000000000000000000000000efa8015fcec7039feb656a4830aa6518bf46010", soliditySha3.GetABIEncodedPacked(new ABIValue("address[2]", list)).ToHex());
        }

        [Fact]
        public virtual void ShouldPackFixedArrayUint8()
        {
            var list = new List<int>(new int[]{ 1 , 2 });
            var soliditySha3 = new ABIEncode();
            Assert.Equal("00000000000000000000000000000000000000000000000000000000000000010000000000000000000000000000000000000000000000000000000000000002", soliditySha3.GetABIEncodedPacked(new ABIValue("uint8[2]", list)).ToHex());
        }


        [Fact]
        public virtual void ShouldPackFixedArrayBytes16()
        {
            var list = new List<byte[]>(new byte[][] {Encoding.UTF8.GetBytes("Hello, world!"), Encoding.UTF8.GetBytes("Hello, world!") });
            var soliditySha3 = new ABIEncode();
            Assert.Equal("48656c6c6f2c20776f726c64210000000000000000000000000000000000000048656c6c6f2c20776f726c642100000000000000000000000000000000000000", soliditySha3.GetABIEncodedPacked(new ABIValue("bytes13[2]", list)).ToHex());
        }

        [Fact]
        public virtual void ShouldPackAddress()
        {
            var soliditySha3 = new ABIEncode();
            Assert.Equal("0x12890D2cce102216644c59daE5baed380d84830c",
                soliditySha3.GetABIEncodedPacked(new ABIValue("address", "0x12890D2cce102216644c59daE5baed380d84830c"))
                    .ToHex(true).ConvertToEthereumChecksumAddress());
        }

        [Theory]
        [InlineData(42, "uint8", "2a")]
        [InlineData(42, "uint16", "002a")]
        [InlineData(42, "uint24", "00002a")]
        [InlineData(42, "uint32", "0000002a")]
        [InlineData(42, "uint40", "000000002a")]
        [InlineData(42, "uint48", "00000000002a")]
        [InlineData(42, "uint56", "0000000000002a")]
        [InlineData(42, "uint64", "000000000000002a")]
        [InlineData(42, "uint72", "00000000000000002a")]
        [InlineData(42, "uint80", "0000000000000000002a")]
        [InlineData(42, "uint88", "000000000000000000002a")]
        [InlineData(42, "uint96", "00000000000000000000002a")]
        [InlineData(42, "uint104", "0000000000000000000000002a")]
        [InlineData(42, "uint112", "000000000000000000000000002a")]
        [InlineData(42, "uint120", "00000000000000000000000000002a")]
        [InlineData(42, "uint128", "0000000000000000000000000000002a")]
        [InlineData(42, "uint136", "000000000000000000000000000000002a")]
        [InlineData(42, "uint144", "00000000000000000000000000000000002a")]
        [InlineData(42, "uint152", "0000000000000000000000000000000000002a")]
        [InlineData(42, "uint160", "000000000000000000000000000000000000002a")]
        [InlineData(42, "uint168", "00000000000000000000000000000000000000002a")]
        [InlineData(42, "uint176", "0000000000000000000000000000000000000000002a")]
        [InlineData(42, "uint184", "000000000000000000000000000000000000000000002a")]
        [InlineData(42, "uint192", "00000000000000000000000000000000000000000000002a")]
        [InlineData(42, "uint200", "0000000000000000000000000000000000000000000000002a")]
        [InlineData(42, "uint208", "000000000000000000000000000000000000000000000000002a")]
        [InlineData(42, "uint216", "00000000000000000000000000000000000000000000000000002a")]
        [InlineData(42, "uint224", "0000000000000000000000000000000000000000000000000000002a")]
        [InlineData(42, "uint232", "000000000000000000000000000000000000000000000000000000002a")]
        [InlineData(42, "uint240", "00000000000000000000000000000000000000000000000000000000002a")]
        [InlineData(42, "uint248", "0000000000000000000000000000000000000000000000000000000000002a")]
        [InlineData(42, "uint256", "000000000000000000000000000000000000000000000000000000000000002a")]
        public virtual void ShouldPackInt(int value, string type, string result)
        {
            var soliditySha3 = new ABIEncode();
            Assert.Equal(result, soliditySha3.GetABIEncodedPacked(new ABIValue(type, value)).ToHex());
        }

        [Fact]
        public virtual void ShouldEncodeSha3UsingDefaultValues()
        {
            var abiEncode = new ABIEncode();
            var result = abiEncode.GetSha3ABIEncodedPacked(234564535,
                "0xfff23243".HexToByteArray(), true, -10);

            Assert.Equal("3e27a893dc40ef8a7f0841d96639de2f58a132be5ae466d40087a2cfa83b7179", result.ToHex());

            var result2 = abiEncode.GetSha3ABIEncodedPacked("Hello!%");
            Assert.Equal("661136a4267dba9ccdf6bfddb7c00e714de936674c4bdb065a531cf1cb15c7fc", result2.ToHex());

            var result3 = abiEncode.GetSha3ABIEncodedPacked(234);
            Assert.Equal("61c831beab28d67d1bb40b5ae1a11e2757fa842f031a2d0bc94a7867bc5d26c2", result3.ToHex());
        }

        [Fact]
        public virtual void ShouldEncodeSha3UsingTypes()
        {
            //0x407D73d8a49eeb85D32Cf465507dd71d507100c1
            var abiEncode = new ABIEncode();
            Assert.Equal("4e8ebbefa452077428f93c9520d3edd60594ff452a29ac7d2ccc11d47f3ab95b",
                abiEncode.GetSha3ABIEncodedPacked(new ABIValue("address", "0x407D73d8a49eeb85D32Cf465507dd71d507100c1"))
                    .ToHex());

            Assert.Equal("4e8ebbefa452077428f93c9520d3edd60594ff452a29ac7d2ccc11d47f3ab95b",
                abiEncode.GetSha3ABIEncodedPacked(new ABIValue("bytes",
                    "0x407D73d8a49eeb85D32Cf465507dd71d507100c1".HexToByteArray())).ToHex());

            //bytes32 it is a 32 bytes array so it will be padded with 00 values
            Assert.Equal("3c69a194aaf415ba5d6afca734660d0a3d45acdc05d54cd1ca89a8988e7625b4",
                abiEncode.GetSha3ABIEncodedPacked(new ABIValue("bytes32",
                    "0x407D73d8a49eeb85D32Cf465507dd71d507100c1".HexToByteArray())).ToHex());

            //web3.utils.soliditySha3({t: 'string', v: 'Hello!%'}, {t: 'int8', v:-23}, {t: 'address', v: '0x85F43D8a49eeB85d32Cf465507DD71d507100C1d'});
            var result =
                abiEncode.GetSha3ABIEncodedPacked(
                    new ABIValue("string", "Hello!%"), new ABIValue("int8", -23),
                    new ABIValue("address", "0x85F43D8a49eeB85d32Cf465507DD71d507100C1d"));

            Assert.Equal("0xa13b31627c1ed7aaded5aecec71baf02fe123797fffd45e662eac8e06fbe4955", result.ToHex(true));
        }

        public class TestParamsInput
        {
            [Parameter("string", 1)] public string First { get; set; }
            [Parameter("int8", 2)] public int Second { get; set; }
            [Parameter("address", 3)] public string Third { get; set; }
        }

        [Fact]
        public virtual void ShouldEncodeParams()
        {
            var abiEncode = new ABIEncode();
            var result = abiEncode.GetSha3ABIParamsEncodedPacked(new TestParamsInput()
                {First = "Hello!%", Second = -23, Third = "0x85F43D8a49eeB85d32Cf465507DD71d507100C1d"});
            Assert.Equal("0xa13b31627c1ed7aaded5aecec71baf02fe123797fffd45e662eac8e06fbe4955", result.ToHex(true));
        }


    }
}