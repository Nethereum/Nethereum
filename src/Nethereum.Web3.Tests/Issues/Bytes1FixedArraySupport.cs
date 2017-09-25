using Nethereum.Hex.HexTypes;
using Nethereum.Web3.Accounts.Managed;
using System.Collections.Generic;
using Xunit;

namespace Nethereum.Web3.Tests.Issues
{
    public class Bytes1FixedArraySupport
    {
        /*
         contract TestBytes1Array {
            byte[50] public testArray;
    
            function initTestArray(){
                uint i = 0;
                while (i < 50) {
                    testArray[i] = 0x01;
                    i++;
                }
            }
    
             function getTestArray() constant  returns (byte[50]) {
                return testArray;
            }
        }
*/
        [Fact]
        public async void ShouldDecodeAnArrayOfBytes1ToASingleArray()
        {
          string ABI = @"[{'constant':true,'inputs':[],'name':'getTestArray','outputs':[{'name':'','type':'bytes1[50]'}],'payable':false,'type':'function'},{'constant':true,'inputs':[{'name':'','type':'uint256'}],'name':'testArray','outputs':[{'name':'','type':'bytes1'}],'payable':false,'type':'function'},{'constant':false,'inputs':[],'name':'initTestArray','outputs':[],'payable':false,'type':'function'}]";
          string BYTE_CODE = "0x6060604052341561000f57600080fd5b5b61025c8061001f6000396000f300606060405263ffffffff7c0100000000000000000000000000000000000000000000000000000000600035041663046401328114610053578063463f5c30146100a0578063e14f4e27146100eb575b600080fd5b341561005e57600080fd5b610066610100565b604051808261064080838360005b8381101561008d5780820151818401525b602001610074565b5050505090500191505060405180910390f35b34156100ab57600080fd5b6100b6600435610188565b6040517fff00000000000000000000000000000000000000000000000000000000000000909116815260200160405180910390f35b34156100f657600080fd5b6100fe6101b3565b005b610108610207565b6000603261064060405190810160405291906106408301826000855b82829054906101000a900460f860020a027effffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff1916815260200190600101906020826000010492830192600103820291508084116101245790505b505050505090505b90565b6000816032811061019557fe5b602091828204019190065b915054906101000a900460f860020a0281565b60005b60328110156102035760f860020a600082603281106101d157fe5b602091828204019190065b6101000a81548160ff021916908360f860020a9004021790555080806001019150506101b6565b5b50565b6106406040519081016040526032815b60008152600019909101906020018161021757905050905600a165627a7a72305820c1584280e4ac0515680eb87cdaf30258541b0078c63cc8cc221288eeefb097d60029";
          var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
          var password = "password";

          var web3 = new Web3(new ManagedAccount(senderAddress, password), ClientFactory.GetClient());

          var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(BYTE_CODE, senderAddress, new HexBigInteger(900000), null);
          var contract = web3.Eth.GetContract(ABI, receipt.ContractAddress);

          var function = contract.GetFunction("getTestArray");
          var result = await function.CallAsync<List<byte>>();

          for(int i = 0; i<50; i++)
          {
                Assert.Equal(0, result[i]);       
          }

          var functionInit = contract.GetFunction("initTestArray");
          receipt = await functionInit.SendTransactionAndWaitForReceiptAsync(senderAddress, new HexBigInteger(900000), null);

          result = await function.CallAsync<List<byte>>();

         for (int i = 0; i < 50; i++)
         {
             Assert.Equal(1, result[i]);
          }
        }

    }
}