using Ethereum.RPC.ABI;
using Xunit;

namespace Ethereum.ABI.Tests.DNX
{
    public class FunctionEncodingTests
    {
        [Fact]
        public virtual void ShouldEncodeInt()
        {
            var functionCallEncoder = new FunctionCallEnconder
            {
                FunctionSha3Encoded = "c6888fa1",
                InputsParams = new[] {CreateParam("int", "a")}
            };
            var result = functionCallEncoder.Encode(69);
            Assert.Equal("0xc6888fa10000000000000000000000000000000000000000000000000000000000000045", result);
        }

        [Fact]
        public virtual void ShouldEncodeAddress()
        {
            var functionCallEncoder = new FunctionCallEnconder
            {
                FunctionSha3Encoded = "c6888fa1",
                InputsParams = new[] {CreateParam("address", "a")}
            };

            var result = functionCallEncoder.Encode("1234567890abcdef1234567890abcdef12345678");
            Assert.Equal("0xc6888fa10000000000000000000000001234567890abcdef1234567890abcdef12345678", result);
        }

        [Fact]
        public virtual void ShouldEncodeBool()
        {
            var functionCallEncoder = new FunctionCallEnconder
            {
                FunctionSha3Encoded = "c6888fa1",
                InputsParams = new[] {CreateParam("bool", "a")}
            };
            var result = functionCallEncoder.Encode(true);
            Assert.Equal("0xc6888fa10000000000000000000000000000000000000000000000000000000000000001", result);
        }

        [Fact]
        public virtual void ShouldEncodeMultipleTypes()
        {
            var functionCallEncoder = new FunctionCallEnconder
            {
                FunctionSha3Encoded = "c6888fa1",
                InputsParams = new[] {CreateParam("address", "a"), CreateParam("int", "b"), CreateParam("int", "c")}
            };
            var result = functionCallEncoder.Encode("1234567890abcdef1234567890abcdef12345678", 69, 69);
            Assert.Equal("0xc6888fa10000000000000000000000001234567890abcdef1234567890abcdef1234567800000000000000000000000000000000000000000000000000000000000000450000000000000000000000000000000000000000000000000000000000000045", result);
        }

       
        [Fact]
        public virtual void ShouldEncodeMultipleTypesIncludingDynamiString()
        {
             var paramsEncoded =
            "0000000000000000000000000000000000000000000000000000000000000060000000000000000000000000000000000000000000000000000000000000004500000000000000000000000000000000000000000000000000000000000000a0000000000000000000000000000000000000000000000000000000000000000568656c6c6f0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005776f726c64000000000000000000000000000000000000000000000000000000";

            var functionCallEncoder = new FunctionCallEnconder
            {
                FunctionSha3Encoded = "c6888fa1",
                InputsParams = new[] {CreateParam("string", "a"), CreateParam("int", "b"), CreateParam("string", "c")}
            };
            var result = functionCallEncoder.Encode("hello", 69, "world");
            Assert.Equal("0x" + functionCallEncoder.FunctionSha3Encoded + paramsEncoded, result);
            
        }

        [Fact]
        public virtual void ShouldEncodeMultipleTypesIncludingDynamicStringAndIntArray()
        {
            var paramsEncoded =
                "00000000000000000000000000000000000000000000000000000000000002c0000000000000000000000000000000000000000000000000000000000003944700000000000000000000000000000000000000000000000000000000000394480000000000000000000000000000000000000000000000000000000000039449000000000000000000000000000000000000000000000000000000000003944a000000000000000000000000000000000000000000000000000000000003944b000000000000000000000000000000000000000000000000000000000003944c000000000000000000000000000000000000000000000000000000000003944d000000000000000000000000000000000000000000000000000000000003944e000000000000000000000000000000000000000000000000000000000003944f0000000000000000000000000000000000000000000000000000000000039450000000000000000000000000000000000000000000000000000000000003945100000000000000000000000000000000000000000000000000000000000394520000000000000000000000000000000000000000000000000000000000039453000000000000000000000000000000000000000000000000000000000003945400000000000000000000000000000000000000000000000000000000000394550000000000000000000000000000000000000000000000000000000000039456000000000000000000000000000000000000000000000000000000000003945700000000000000000000000000000000000000000000000000000000000394580000000000000000000000000000000000000000000000000000000000039459000000000000000000000000000000000000000000000000000000000003945a0000000000000000000000000000000000000000000000000000000000000300000000000000000000000000000000000000000000000000000000000000000568656c6c6f0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005776f726c64000000000000000000000000000000000000000000000000000000";

            var functionCallEncoder = new FunctionCallEnconder
            {
                FunctionSha3Encoded = "c6888fa1",
                InputsParams =
                    new[] {CreateParam("string", "a"), CreateParam("uint[20]", "b"), CreateParam("string", "c")}
            };

            var array = new uint[20];
            for (uint i = 0; i < 20; i++)
            {
                array[i] = i + 234567;
            }

            var result = functionCallEncoder.Encode("hello", array, "world");

            Assert.Equal("0x" + functionCallEncoder.FunctionSha3Encoded + paramsEncoded, result);
          
        }

        public Param CreateParam(string type, string name)
        {
            return new Param() {Type = ABIType.CreateABIType(type), Name = name};
        }
    }
}