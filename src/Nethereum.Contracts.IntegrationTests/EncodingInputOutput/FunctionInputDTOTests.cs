using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.Extensions;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.EncodingInputOutput
{
    public class FunctionInputDTOTests
    {

        [Function("sample")]
        public class SampleFunction : FunctionMessage
        {
            [Parameter("address", "_address1", 1)]
            public virtual string Address1 { get; set; }
        }

        [Fact]
        public virtual void WhenAnAddressParameterValueIsNull_ShouldProvideAHelpfulError()
        {
            var incompleteFunction = new SampleFunction();
            var ex = Assert.Throws<Exception>(() => incompleteFunction.GetParamsEncoded());

            const string ExpectedError =
                "An error occurred encoding parameter value. Parameter Order: '1', Name: '_address1', Value: 'null'.  Ensure the value is valid for the parameter type.";

            Assert.Equal(ExpectedError, ex.Message);
        }
    }
}
