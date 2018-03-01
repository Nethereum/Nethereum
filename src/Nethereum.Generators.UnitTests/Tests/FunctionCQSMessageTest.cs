using Nethereum.Generators.CQS;
using Xunit;

namespace Nethereum.Generators.Tests
{
    public class FunctionCQSMessageTest
    {
        string abi = @"[{
                     'constant': true,
                    'inputs': [
                    {
                        'name': '_number',
                        'type': 'uint256'
                    }
                    ],
                    'name': 'baseStats',
                    'outputs': [
                    {
                        'name': 'hp',
                        'type': 'uint16'
                    },
                    {
                        'name': 'attack',
                        'type': 'uint16'
                    },
                    {
                    'name': 'defense',
                    'type': 'uint16'
                    },
                    {
                    'name': 'spAttack',
                    'type': 'uint16'
                    },
                    {
                    'name': 'spDefense',
                    'type': 'uint16'
                    },
                    {
                    'name': 'speed',
                    'type': 'uint16'
                    }
                    ],
                    'payable': false,
                    'stateMutability': 'view',
                    'type': 'function'
                    }]";

        

        private static string expectedClassOutput =
            @"    [Function(""baseStats"", typeof(BaseStatsOutputDTO))]
    public class BaseStatsFunction:ContractMessage
    {
        [Parameter(""uint256"", ""_number"", 1)]
        public BigInteger Number {get; set;}
    }";

        private static string namespaceName = "DefaultNamespace";
        private static string functionOutputNamespaceName = "DefaultNamespace.FunctionOutput";

        private string expectedClassFullOutput =
            $@"using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using {functionOutputNamespaceName};
namespace {namespaceName}
{{
{expectedClassOutput}
}}
";

        [Fact]
        public void ShouldGenerateFunctionOutputDTOClass()
        {
            var service = new FunctionCQSMessageGenerator();
            var output = service.GenerateClass(abi);
            Assert.Equal(expectedClassOutput, output);
        }

        [Fact]
        public void ShouldGenerateFunctionOutputDTOFullClass()
        {
            var service = new FunctionCQSMessageGenerator();
            var output = service.GenerateFullClass(abi, namespaceName, functionOutputNamespaceName);
            Assert.Equal(expectedClassFullOutput, output);
        }

    }
}