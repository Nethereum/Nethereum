using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.JsonDeserialisation;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Linq;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class AbiEncodeTests
    {
        [Fact]
        public virtual void ShouldEncodeMultipleTypesIncludingDynamicString()
        {
            var paramsEncoded =
                "0000000000000000000000000000000000000000000000000000000000000060000000000000000000000000000000000000000000000000000000000000004500000000000000000000000000000000000000000000000000000000000000a0000000000000000000000000000000000000000000000000000000000000000568656c6c6f0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005776f726c64000000000000000000000000000000000000000000000000000000";
            var abiEncode = new ABIEncode();
            var result = abiEncode.GetABIEncoded(new ABIValue("string", "hello"), new ABIValue("int", 69),
                new ABIValue("string", "world"));

            Assert.Equal("0x" + paramsEncoded, result.ToHex(true));
        }

        [Fact]
        public virtual void ShouldEncodeMultipleValuesUsingDefaultConvertors()
        {
            var paramsEncoded =
                "000000000000000000000000000000000000000000000000000000000000006000000000000000000000000000000000000000000000000000000000000000a000000000000000000000000000000000000000000000000000000000000000e0000000000000000000000000000000000000000000000000000000000000000131000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001320000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000013300000000000000000000000000000000000000000000000000000000000000";
            var abiEncode = new ABIEncode();
            var encoded = abiEncode.GetABIEncoded("1", "2", "3").ToHex();
            Assert.Equal(paramsEncoded, encoded);
        }

        [Fact]
        public virtual void ShouldEncodeParams()
        {
            var paramsEncoded =
                "0000000000000000000000000000000000000000000000000000000000000060000000000000000000000000000000000000000000000000000000000000004500000000000000000000000000000000000000000000000000000000000000a0000000000000000000000000000000000000000000000000000000000000000568656c6c6f0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005776f726c64000000000000000000000000000000000000000000000000000000";
            var abiEncode = new ABIEncode();
            var result = abiEncode.GetABIParamsEncoded(new TestParamsInput(){First = "hello", Second = 69, Third = "world"});
            Assert.Equal("0x" + paramsEncoded, result.ToHex(true));
        }


        [Fact]
        public virtual void ShouldEncodePackedArrayAddressType()
        {
            var paramsEncoded = "0x0000000000000000000000007dd31bc2ffa37ab492a8d60f9c7170b78f12e1c10000000000000000000000000efa8015fcec7039feb656a4830aa6518bf46011";

            var addreses = new string[] { "0x7Dd31bc2ffA37Ab492a8d60F9C7170B78f12E1c1", "0x0efa8015fcec7039feb656a4830aa6518bf46011" };
            var abiEncode = new ABIEncode();
            var result = abiEncode.GetABIEncodedPacked(new ABIValue("address[]", addreses));
            Assert.Equal(paramsEncoded, result.ToHex(true));
        }

        public class TestParamsInput
        {
            [Parameter("string", 1)]
            public string First { get; set; }
            [Parameter("int256", 2)]
            public int Second { get; set; }
            [Parameter("string", 3)]
            public string Third { get; set; }
        }

    }
}