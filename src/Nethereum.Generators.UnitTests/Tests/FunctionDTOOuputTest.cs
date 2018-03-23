using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Generators.DTOs;
using Xunit;

namespace Nethereum.Generators.Tests
{
    public class FunctionDTOOuputTest
    {
        string abi= @"[{
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
@"    [FunctionOutput]
    public class BaseStatsOutputDTO
    {
        [Parameter(""uint16"", ""hp"", 1)]
        public short Hp {get; set;}
        [Parameter(""uint16"", ""attack"", 2)]
        public short Attack {get; set;}
        [Parameter(""uint16"", ""defense"", 3)]
        public short Defense {get; set;}
        [Parameter(""uint16"", ""spAttack"", 4)]
        public short SpAttack {get; set;}
        [Parameter(""uint16"", ""spDefense"", 5)]
        public short SpDefense {get; set;}
        [Parameter(""uint16"", ""speed"", 6)]
        public short Speed {get; set;}
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
            //var service = new FunctionOutputDTOGenerator();
            //var output = service.GenerateClass(abi);
            //Assert.Equal(expectedClassOutput, output);
            Assert.True(false, "TO DO");
        }

        [Fact]
        public void ShouldGenerateFunctionOutputDTOFullClass()
        {
            //var service = new FunctionOutputDTOGenerator();
            //var output = service.GenerateFullClass(abi, namespaceName);
            //Assert.Equal(expectedClassFullOutput, output);
            Assert.True(false, "TO DO");
        }

    }
}
