using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.Web3.Sample
{
    public class IntTypeIntegrationTests
    {
        public async Task<string> Test()
        {
            //The compiled solidity contract to be deployed
            /*
              contract test { 
    
    
    function test1() returns(int) { 
       int d = 3457987492347979798742;
       return d;
    }
    
      function test2(int d) returns(int) { 
       return d;
    }
    
    function test3(int d)returns(int){
        int x = d + 1 -1;
        return x;
    }
    
    function test4(int d)returns(bool){
        return d == 3457987492347979798742;
    }
    
    function test5(int d)returns(bool){
        return d == -3457987492347979798742;
    }
    
    function test6(int d)returns(bool){
        return d == 500;
    }
    
    function test7(int256 d)returns(bool){
        return d == 74923479797565;
    }
    
    function test8(int256 d)returns(bool){
        return d == 9223372036854775808;
    }
}
           }
           */

            var contractByteCode =
                "60606040526102b7806100126000396000f36060604052361561008a576000357c01000000000000000000000000000000000000000000000000000000009004806311da9d8c1461008c5780631c2a1101146100b857806363798981146100e45780636b59084d146101105780639e71212514610133578063a605861c1461015f578063e42d455b1461018b578063e92b09da146101b75761008a565b005b6100a26004808035906020019091905050610243565b6040518082815260200191505060405180910390f35b6100ce600480803590602001909190505061020e565b6040518082815260200191505060405180910390f35b6100fa60048080359060200190919050506101ff565b6040518082815260200191505060405180910390f35b61011d60048050506101e3565b6040518082815260200191505060405180910390f35b6101496004808035906020019091905050610229565b6040518082815260200191505060405180910390f35b6101756004808035906020019091905050610274565b6040518082815260200191505060405180910390f35b6101a1600480803590602001909190505061029e565b6040518082815260200191505060405180910390f35b6101cd6004808035906020019091905050610287565b6040518082815260200191505060405180910390f35b6000600068bb75377716692498d690508091506101fb565b5090565b6000819050610209565b919050565b60006000600160018401039050809150610223565b50919050565b600068bb75377716692498d68214905061023e565b919050565b60007fffffffffffffffffffffffffffffffffffffffffffffff448ac888e996db672a8214905061026f565b919050565b60006101f482149050610282565b919050565b60006544247b660f3d82149050610299565b919050565b6000678000000000000000821490506102b2565b91905056";

            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""d"",""type"":""int256""}],""name"":""test5"",""outputs"":[{""name"":"""",""type"":""bool""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""d"",""type"":""int256""}],""name"":""test3"",""outputs"":[{""name"":"""",""type"":""int256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""d"",""type"":""int256""}],""name"":""test2"",""outputs"":[{""name"":"""",""type"":""int256""}],""type"":""function""},{""constant"":false,""inputs"":[],""name"":""test1"",""outputs"":[{""name"":"""",""type"":""int256""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""d"",""type"":""int256""}],""name"":""test4"",""outputs"":[{""name"":"""",""type"":""bool""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""d"",""type"":""int256""}],""name"":""test6"",""outputs"":[{""name"":"""",""type"":""bool""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""d"",""type"":""int256""}],""name"":""test8"",""outputs"":[{""name"":"""",""type"":""bool""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""d"",""type"":""int256""}],""name"":""test7"",""outputs"":[{""name"":"""",""type"":""bool""}],""type"":""function""}]";

            var addressFrom = "0x12890d2cce102216644c59dae5baed380d84830c";

            var web3 = new Web3();

          
            var pass = "password";
            var result = await web3.Personal.UnlockAccount.SendRequestAsync(addressFrom, pass, new HexBigInteger(600));

            //deploy the contract, including abi and a paramter of 7. 
            var transactionHash =
                await
                    web3.Eth.DeployContract.SendRequestAsync(contractByteCode, addressFrom,
                        new HexBigInteger(900000));

            //get the contract address 
            TransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                await Task.Delay(500);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);
            var test1 = contract.GetFunction("test1");
            Assert.Equal("3457987492347979798742", (await test1.CallAsync<BigInteger>()).ToString());
            var test2 = contract.GetFunction("test2");
            Assert.Equal("3457987492347979798742", (await test2.CallAsync<BigInteger>(BigInteger.Parse("3457987492347979798742"))).ToString());

            var test3 = contract.GetFunction("test3");
            Assert.Equal("3457987492347979798742", (await test3.CallAsync<BigInteger>(BigInteger.Parse("3457987492347979798742"))).ToString());

            var test4 = contract.GetFunction("test4");
            Assert.True(await test4.CallAsync<bool>(BigInteger.Parse("3457987492347979798742")));

            var test5 = contract.GetFunction("test5");
            Assert.True(await test5.CallAsync<bool>(BigInteger.Parse("-3457987492347979798742")));

            var test6 = contract.GetFunction("test6");
            Assert.True(await test6.CallAsync<bool>(BigInteger.Parse("500")));

            var test8 = contract.GetFunction("test8");
            Assert.True(await test8.CallAsync<bool>(BigInteger.Parse("9223372036854775808")));




            return "OK";
        }
    }
}