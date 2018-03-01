using Nethereum.Generator.Console;
using Xunit;

namespace Nethereum.Generators.Tests
{
    public class EventDTOTest
    {
        string abi = @"[{'anonymous':false,'inputs':[{'indexed':false,'name':'currentState','type':'uint256'},{'indexed':false,'name':'newState','type':'uint256'},{'indexed':false,'name':'time','type':'uint256'}],'name':'StateChanged','type':'event'}]";


        private static string expectedClassOutput =
            @"    [Event(""StateChanged"")]
    public class StateChangedEventDTO
    {
        [Parameter(""uint256"", ""currentState"", 1, false )]
        public BigInteger CurrentState {get; set;}
        [Parameter(""uint256"", ""newState"", 2, false )]
        public BigInteger NewState {get; set;}
        [Parameter(""uint256"", ""time"", 3, false )]
        public BigInteger Time {get; set;}
    }";

        private static string namespaceName = "DefaultNamespace";

        private string expectedClassFullOutput =
            $@"using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace {namespaceName}
{{
{expectedClassOutput}
}}
";

        [Fact]
        public void ShouldGenerateFunctionOutputDTOClass()
        {
            var service = new EventDTOGenerator();
            var output = service.GenerateClass(abi);
            Assert.Equal(expectedClassOutput, output);
        }

        [Fact]
        public void ShouldGenerateFunctionOutputDTOFullClass()
        {
            var service = new EventDTOGenerator();
            var output = service.GenerateFullClass(abi, namespaceName);
            Assert.Equal(expectedClassFullOutput, output);
        }

    }
}