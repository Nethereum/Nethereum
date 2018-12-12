using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Nethereum.ABI.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.ABI.UnitTests
{

    public class ABIValue
    {
        public ABIValue(ABIType abiType, object value)
        {
            ABIType = abiType;
            Value = value;
        }

        public ABIValue(string abiType, object value)
        {
            ABIType = ABIType.CreateABIType(abiType);
            Value = value;
        }

        public ABIType ABIType { get; set; }
        public object Value { get; set; }
    }

    public class ABIEncode
    {

        public byte[] GetSha3ABIPacked(params ABIValue[] abiValues)
        {
            return new Sha3Keccack().CalculateHash(GetABIPacked(abiValues));
        }

        public byte[] GetSha3ABIPacked(params object[] values)
        {
            return new Sha3Keccack().CalculateHash(GetABIPacked(values));
        }

        public byte[] GetABIPacked(params ABIValue[] abiValues)
        {
            var result = new List<byte>();
            foreach (var abiValue in abiValues)
            {
                result.AddRange(abiValue.ABIType.EncodePacked(abiValue.Value));
            }
            return result.ToArray();
        }

        public byte[] GetABIPacked(params object[] values)
        {
            var abiValues = new List<ABIValue>();
            foreach (var value in values)
            {
                if (value.IsNumber())
                {
                    var bigInt = BigInteger.Parse(value.ToString());
                    if (bigInt >= 0)
                    {
                        abiValues.Add(new ABIValue(new IntType("uint256"), value));
                    }
                    else
                    {
                        abiValues.Add(new ABIValue(new IntType("int256"), value));
                    }
                }

                if (value is string)
                {
                    abiValues.Add(new ABIValue(new StringType(), value));
                }

                if (value is bool)
                {
                    abiValues.Add(new ABIValue(new BoolType(), value));
                }

                if (value is byte[])
                {
                    abiValues.Add(new ABIValue(new BytesType(), value));
                }
            }
            return GetABIPacked(abiValues.ToArray());
        }
        
    }

    public class ABIPackingTests
    {

        [Fact]
        public virtual void ShouldPackBool()
        {
            var soliditySha3 = new ABIEncode();
            Assert.Equal("01", soliditySha3.GetABIPacked(true).ToHex());
            Assert.Equal("00", soliditySha3.GetABIPacked(false).ToHex());
        }

        [Fact]
        public virtual void ShouldPackAddress()
        {
            var soliditySha3 = new ABIEncode();
            Assert.Equal("0x12890D2cce102216644c59daE5baed380d84830c", soliditySha3.GetABIPacked(new ABIValue("address", "0x12890D2cce102216644c59daE5baed380d84830c")).ToHex(true).ConvertToEthereumChecksumAddress());
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
            Assert.Equal(result, soliditySha3.GetABIPacked(new ABIValue(type, value)).ToHex());
        }

        [Fact]
        public virtual void ShouldEncodeSha3UsingDefaultValues()
        {
            var soliditySha3 = new ABIEncode();
            var result = soliditySha3.GetSha3ABIPacked(234564535,
                "0xfff23243".HexToByteArray(), true, -10);

            Assert.Equal("3e27a893dc40ef8a7f0841d96639de2f58a132be5ae466d40087a2cfa83b7179", result.ToHex());

            var result2 = soliditySha3.GetSha3ABIPacked("Hello!%");
            Assert.Equal("661136a4267dba9ccdf6bfddb7c00e714de936674c4bdb065a531cf1cb15c7fc", result2.ToHex());

           var result3 = soliditySha3.GetSha3ABIPacked(234);
           Assert.Equal("61c831beab28d67d1bb40b5ae1a11e2757fa842f031a2d0bc94a7867bc5d26c2", result3.ToHex());
        }

        [Fact]
        public virtual void ShouldEncodeSha3UsingTypes()
        {
            //0x407D73d8a49eeb85D32Cf465507dd71d507100c1
            var abiEncode = new ABIEncode();
            Assert.Equal("4e8ebbefa452077428f93c9520d3edd60594ff452a29ac7d2ccc11d47f3ab95b",
                abiEncode.GetSha3ABIPacked(new ABIValue("address", "0x407D73d8a49eeb85D32Cf465507dd71d507100c1")).ToHex());

            Assert.Equal("4e8ebbefa452077428f93c9520d3edd60594ff452a29ac7d2ccc11d47f3ab95b",
                abiEncode.GetSha3ABIPacked(new ABIValue("bytes", "0x407D73d8a49eeb85D32Cf465507dd71d507100c1".HexToByteArray())).ToHex());

            //bytes32 it is a 32 bytes array so it will be padded with 00 values
            Assert.Equal("3c69a194aaf415ba5d6afca734660d0a3d45acdc05d54cd1ca89a8988e7625b4",
                abiEncode.GetSha3ABIPacked(new ABIValue("bytes32", "0x407D73d8a49eeb85D32Cf465507dd71d507100c1".HexToByteArray())).ToHex());
            
            //web3.utils.soliditySha3({t: 'string', v: 'Hello!%'}, {t: 'int8', v:-23}, {t: 'address', v: '0x85F43D8a49eeB85d32Cf465507DD71d507100C1d'});
            var result =
                abiEncode.GetSha3ABIPacked(
                    new ABIValue("string", "Hello!%"), new ABIValue("int8", -23),
                    new ABIValue("address", "0x85F43D8a49eeB85d32Cf465507DD71d507100C1d"));

            Assert.Equal("0xa13b31627c1ed7aaded5aecec71baf02fe123797fffd45e662eac8e06fbe4955", result.ToHex(true));
        }
    }

    public class AddressEncodingTests
    {
        [Fact]
        public virtual void ShouldDecodeAddressString()
        {
            var addressType = new AddressType();
            var result2 =
                addressType.Decode("0000000000000000000000001234567890abcdef1234567890abcdef12345678".HexToByteArray(),
                    typeof(string));
            Assert.Equal("0x1234567890abcdef1234567890abcdef12345678", result2);
        }

        [Fact]
        public virtual void ShouldEncodeAddressString()
        {
            var addressType = new AddressType();
            var result2 = addressType.Encode("1234567890abcdef1234567890abcdef12345678").ToHex();
            Assert.Equal("0000000000000000000000001234567890abcdef1234567890abcdef12345678", result2);
        }


        [Fact]
        public virtual void ShouldEncodeDecodeAddressString()
        {
            var addressType = new AddressType();
            var result2 = addressType.Encode("0034567890abcdef1234567890abcdef12345678").ToHex();
            var result3 = addressType.Decode(result2, typeof(string));
            Assert.Equal("0x0034567890abcdef1234567890abcdef12345678", result3);
        }
    }
}