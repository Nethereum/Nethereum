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
        public void ShouldExtractSignatures()
        {
            //string fullSignature = "function addPerson(tuple(string name, uint16 age) person)";
            //string fullSignature = "function addPeople(tuple(string name, uint16 age)[] person)";
            //string fullSignature = @" function balanceOf ( address owner, address thing ) public view returns (uint)
            //                     function setCompleted(uint completed) public restricted 
            //                     constructor(address erc20)
            //                     event FeePercentUpdated(uint256 indexed newFeePercent)
            //                     event HarvestDelayUpdated(uint64 newHarvestDelay)";
            var contractAbi = new ABIStringSignatureDeserialiser().ExtractContractABI(fullSignature);
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