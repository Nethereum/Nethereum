using Nethereum.ABI.FunctionEncoding;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class FunctionDecodingTests
    {

        [Fact]
        public void WhenThereAreNoInputParametersDecodingShouldReturnTheUnalteredFunctionInputDTO()
        {
            const string signature = "0x82692679"; //function name = doSomething
            const string data = signature;
        
            var decoder = new FunctionCallDecoder();
            var functionInput = new object();

            var decodedFunctionInput = decoder.DecodeFunctionInput(functionInput, signature, data);
            Assert.Equal(functionInput, decodedFunctionInput);
        }
    }
}
