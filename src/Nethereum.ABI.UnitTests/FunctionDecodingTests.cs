using System;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
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

        public class FunctionOutputWithoutAttribute
        {
            [Parameter("string", "_value", 1)]
            public string Value { get; set; }
        }

        [FunctionOutput]
        public class FunctionOutputWithAttribute : IFunctionOutputDTO
        {
            [Parameter("string", "_value", 1)]
            public string Value { get; set; }
        }

        [Fact]
        public void WhenTypeDoesNotApplyFunctionOutputAttribute_ThrowsExpectedError()
        {
            //ensure the expected error IS thrown when attribute is not specified
            var exception = Assert.Throws<ArgumentException>(() =>
                new FunctionCallDecoder().DecodeFunctionOutput<FunctionOutputWithoutAttribute>( string.Empty));

            Assert.Equal("Unable to decode to 'FunctionOutputWithoutAttribute' because the type does not apply attribute '[FunctionOutputAttribute]'.", exception.Message);
        }

        [Fact]
        public void WhenTypeDoesApplyFunctionOutputAttribute_ReturnsInstanceOfType()
        {
            //ensure an error is NOT throw when attribute IS specified
            var decodedResult = new FunctionCallDecoder().DecodeFunctionOutput<FunctionOutputWithAttribute>("");
            Assert.NotNull(decodedResult);
        }
    }
}
