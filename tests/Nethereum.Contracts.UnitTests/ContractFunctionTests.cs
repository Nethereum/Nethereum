using Xunit;

namespace Nethereum.Contracts.UnitTests
{
    public class ContractFunctionTests
    {
        [Fact]
        public void ShouldAllowToCreateFunctionsWithoutAnApi()
        {
            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""nextOwner"",""type"":""address""}],""name"":""setNextOwner"",""outputs"":[{""name"":""set"",""type"":""bool""}],""payable"":false,""type"":""function""},{""constant"":true,""inputs"":[],""name"":""getProduct"",""outputs"":[{""name"":""product"",""type"":""string""}],""payable"":false,""type"":""function""},{""constant"":true,""inputs"":[],""name"":""getOwner"",""outputs"":[{""name"":""owner"",""type"":""address""}],""payable"":false,""type"":""function""},{""constant"":true,""inputs"":[],""name"":""getOwners"",""outputs"":[{""name"":""owners"",""type"":""address[]""}],""payable"":false,""type"":""function""},{""constant"":false,""inputs"":[],""name"":""confirmOwnership"",""outputs"":[{""name"":""confirmed"",""type"":""bool""}],""payable"":false,""type"":""function""},{""inputs"":[{""name"":""productDigest"",""type"":""string""}],""type"":""constructor""}]";

            var contract = new Contract(null, abi, "0x99");

            var function = contract.GetFunction("getProduct");
            Assert.NotNull(function);
        } 
    }
}