using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class JsonDecodingTests
    {
        [Fact]
        public void ShouldDecodeStringArrays()
        {
            var parameterOutput = new ParameterOutput();
            parameterOutput.Parameter = new Parameter("string[2]", "result");
            parameterOutput.Result = new List<string>(new string[] { "1", "two" });
            parameterOutput.DataIndexStart = 32;
            var parsedResult = new List<ParameterOutput>(new[] { parameterOutput }).ConvertToJObject();
            var expectedJObject = JObject.Parse(@"{'result': ['1', 'two']}");
            Assert.True(JToken.DeepEquals(expectedJObject, parsedResult));
        }

    }
}