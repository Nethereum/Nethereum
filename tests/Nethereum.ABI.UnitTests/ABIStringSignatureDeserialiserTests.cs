using Nethereum.ABI.Model;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class ABIStringSignatureDeserialiserTests
    {
        
        [Fact]
        public void ShouldExtractEvent()
        {
            string signature = "event FeePercentUpdated(uint256 newFeePercent))";
            var contractAbi = new ABIStringSignatureDeserialiser().ExtractContractABI(signature);
            var eventAbi = contractAbi.Events[0];
            Assert.Equal("FeePercentUpdated", eventAbi.Name);
            Assert.Equal("newFeePercent", eventAbi.InputParameters[0].Name);
            Assert.Equal("uint256", eventAbi.InputParameters[0].Type);
            Assert.False(eventAbi.InputParameters[0].Indexed);
        }

        [Fact]
        public void ShouldExtractEventIndexed()
        {
            string signature = "event FeePercentUpdated(uint256 indexed newFeePercent))";
            var contractAbi = new ABIStringSignatureDeserialiser().ExtractContractABI(signature);
            var eventAbi = contractAbi.Events[0];
            Assert.Equal("FeePercentUpdated", eventAbi.Name);
            Assert.Equal("newFeePercent", eventAbi.InputParameters[0].Name);
            Assert.Equal("uint256", eventAbi.InputParameters[0].Type);
            Assert.True(eventAbi.InputParameters[0].Indexed);
        }

        [Fact]
        public void ShouldExtractConstructor()
        {
            string signature = "constructor(address erc20)";
            var contractAbi = new ABIStringSignatureDeserialiser().ExtractContractABI(signature);
            var constructor = contractAbi.Constructor;
            Assert.Equal("erc20", constructor.InputParameters[0].Name);
            Assert.Equal("address", constructor.InputParameters[0].Type);
        }

        [Fact]
        public void ShouldExtractFunctionWithTuple()
        {
            string signature = "function getTuplePerson(uint id) view returns (tuple(string name, string surname, uint16 age))";
            var contractAbi = new ABIStringSignatureDeserialiser().ExtractContractABI(signature);
            var function = contractAbi.Functions[0];
            Assert.Equal("getTuplePerson", function.Name);
            Assert.Equal("id", function.InputParameters[0].Name);
            Assert.Equal("uint", function.InputParameters[0].Type);
            Assert.Equal("tuple", function.OutputParameters[0].Type);
            
            var tupleType = function.OutputParameters[0].ABIType as TupleType;
            Assert.Equal("name", tupleType.Components[0].Name);
            Assert.Equal("string", tupleType.Components[0].Type);
            Assert.Equal("surname", tupleType.Components[1].Name);
            Assert.Equal("string", tupleType.Components[1].Type);
            Assert.Equal("age", tupleType.Components[2].Name);
            Assert.Equal("uint16", tupleType.Components[2].Type);
            
        }

        [Fact]
        public void ShouldExtractFunctionWithTupleArray()
        {
            string signature = "function getTuplePerson(uint id) view returns (tuple(string name, string surname, uint16 age)[])";
            var contractAbi = new ABIStringSignatureDeserialiser().ExtractContractABI(signature);
            var function = contractAbi.Functions[0];
            Assert.Equal("getTuplePerson", function.Name);
            Assert.Equal("id", function.InputParameters[0].Name);
            Assert.Equal("uint", function.InputParameters[0].Type);
            Assert.Equal("tuple[]", function.OutputParameters[0].Type);

            var arrayType = function.OutputParameters[0].ABIType as ArrayType;
            Assert.NotNull(arrayType);
            //assert element...
        }

        [Fact]
        public void ShouldExtractFunctionWithTupleJustParenthesis()
        {
            string signature = "function getTuplePerson(uint id) view returns ((string name, string surname, uint16 age))";
            var contractAbi = new ABIStringSignatureDeserialiser().ExtractContractABI(signature);
            var function = contractAbi.Functions[0];
            Assert.Equal("getTuplePerson", function.Name);
            Assert.Equal("id", function.InputParameters[0].Name);
            Assert.Equal("uint", function.InputParameters[0].Type);
            Assert.Equal("tuple", function.OutputParameters[0].Type);

            var tupleType = function.OutputParameters[0].ABIType as TupleType;
            Assert.Equal("name", tupleType.Components[0].Name);
            Assert.Equal("string", tupleType.Components[0].Type);
            Assert.Equal("surname", tupleType.Components[1].Name);
            Assert.Equal("string", tupleType.Components[1].Type);
            Assert.Equal("age", tupleType.Components[2].Name);
            Assert.Equal("uint16", tupleType.Components[2].Type);

        }


        [Fact]
        public void ShouldExtractFunction()
        {
            string signature = " function balanceOf ( address owner, address thing ) public view returns (uint)";
            var contractAbi = new ABIStringSignatureDeserialiser().ExtractContractABI(signature);
            var function = contractAbi.Functions[0];
            Assert.Equal("balanceOf", function.Name);
            Assert.True(function.Constant);
            Assert.Equal("owner", function.InputParameters[0].Name);
            Assert.Equal("address", function.InputParameters[0].Type);
            Assert.Equal("thing", function.InputParameters[1].Name);
            Assert.Equal("address", function.InputParameters[1].Type);
            Assert.Equal("uint", function.OutputParameters[0].Type);

        }

    }
}