using Ethereum.RPC.Sample.ContractTest;
using Xunit;

namespace Ethereum.ABI.Tests.DNX
{
    public class FunctionEncodingTests
    {
        [Fact]
        public virtual void ShouldEncodeInt()
        {
            var functionCallEncoder = new FunctionCallEnconder();
            functionCallEncoder.FunctionSha3Encoded = "c6888fa1";
            functionCallEncoder.FunctionTypes = new string[] {"int"};
            var result = functionCallEncoder.Encode(69);
            Assert.Equal("0xc6888fa10000000000000000000000000000000000000000000000000000000000000045", result);
        }

        [Fact]
        public virtual void ShouldEncodeAddress()
        {
            var functionCallEncoder = new FunctionCallEnconder();
            functionCallEncoder.FunctionSha3Encoded = "c6888fa1";
            functionCallEncoder.FunctionTypes = new string[] { "address" };
            var result = functionCallEncoder.Encode("1234567890abcdef1234567890abcdef12345678");
            Assert.Equal("0xc6888fa10000000000000000000000001234567890abcdef1234567890abcdef12345678", result);
        }

        [Fact]
        public virtual void ShouldEncodeBool()
        {
            var functionCallEncoder = new FunctionCallEnconder();
            functionCallEncoder.FunctionSha3Encoded = "c6888fa1";
            functionCallEncoder.FunctionTypes = new string[] { "bool" };
            var result = functionCallEncoder.Encode(true);
            Assert.Equal("0xc6888fa10000000000000000000000000000000000000000000000000000000000000001", result);
        }

        [Fact]
        public virtual void ShouldEncodeMultipleTypes()
        {
            var functionCallEncoder = new FunctionCallEnconder();
            functionCallEncoder.FunctionSha3Encoded = "c6888fa1";
            functionCallEncoder.FunctionTypes = new string[] { "address", "int", "int" };
            var result = functionCallEncoder.Encode("1234567890abcdef1234567890abcdef12345678", 69, 69);
            Assert.Equal("0xc6888fa10000000000000000000000001234567890abcdef1234567890abcdef1234567800000000000000000000000000000000000000000000000000000000000000450000000000000000000000000000000000000000000000000000000000000045", result);
        }
    }
}