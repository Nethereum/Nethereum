using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Contracts.Services;
using Xunit;

namespace Nethereum.Contracts.UnitTests
{
    public class FunctionEncodingDecoding
    {
        [Fact]
        public void ShouldDecodeInt()
        {
            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""}]";


            var ethApi = new EthApiContractService(null, (RPC.TransactionManagers.TransactionManager)null);


            var contract = ethApi.GetContract(abi, "ContractAddress");

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");
            var data = multiplyFunction.GetData(69);
            var decode = multiplyFunction.DecodeInput(data);
            Assert.Equal(69, (BigInteger) decode[0].Result);
        }

        [Fact]
        public void ShouldDecodeMultipleParamsIncludingArray()
        {
            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""},{""name"":""b"",""type"":""string""},{""name"":""c"",""type"":""uint[3]""} ],""name"":""test"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""}]";

            var ethApi = new EthApiContractService(null, (RPC.TransactionManagers.TransactionManager)null);

            var contract = ethApi.GetContract(abi, "ContractAddress");

            //get the function by name
            var testFunction = contract.GetFunction("test");
            var array = new[] {1, 2, 3};

            var str = "hello";
            var data = testFunction.GetData(69, str, array);
            var decode = testFunction.DecodeInput(data);

            Assert.Equal(69, (BigInteger) decode[0].Result);
            Assert.Equal(str, (string) decode[1].Result);

            var listObjects = decode[2].Result as List<object>;

            if (listObjects != null)
            {
                //System.Array.ConvertAll(listObjects.ToArray(), x => (int)((BigInteger)x));
                var newArray = listObjects.Select(x => (int) (BigInteger) x).ToArray();
                Assert.Equal(array, newArray);
            }
        }
    }
}