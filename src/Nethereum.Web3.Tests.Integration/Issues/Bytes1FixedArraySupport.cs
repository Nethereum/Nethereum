using Nethereum.Hex.HexTypes;
using Nethereum.Web3.Accounts.Managed;
using System.Collections.Generic;
using Xunit;

namespace Nethereum.Web3.Tests.Integration.Issues
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

        function initTestArrayExternally(byte[50] array){
            testArray = array;
        }
    }
*/
        [Fact]
        public async void ShouldEncodeDecodeAnArrayOfBytes1ToASingleArray()
        {
            string ABI = @"[{'constant':true,'inputs':[],'name':'getTestArray','outputs':[{'name':'','type':'bytes1[50]'}],'payable':false,'type':'function'},{'constant':true,'inputs':[{'name':'','type':'uint256'}],'name':'testArray','outputs':[{'name':'','type':'bytes1'}],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'array','type':'bytes1[50]'}],'name':'initTestArrayExternally','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[],'name':'initTestArray','outputs':[],'payable':false,'type':'function'}]";
            string BYTE_CODE = "0x6060604052341561000f57600080fd5b5b6103738061001f6000396000f300606060405263ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166304640132811461005e578063463f5c30146100ab578063c4dd6a55146100f6578063e14f4e2714610134575b600080fd5b341561006957600080fd5b610071610149565b604051808261064080838360005b838110156100985780820151818401525b60200161007f565b5050505090500191505060405180910390f35b34156100b657600080fd5b6100c16004356101d1565b6040517fff00000000000000000000000000000000000000000000000000000000000000909116815260200160405180910390f35b341561010157600080fd5b61013260046106448160326106406040519081016040529190828261064080828437509395506101fc945050505050565b005b341561013f57600080fd5b61013261020e565b005b610151610262565b6000603261064060405190810160405291906106408301826000855b82829054906101000a900460f860020a027effffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff19168152602001906001019060208260000104928301926001038202915080841161016d5790505b505050505090505b90565b600081603281106101de57fe5b602091828204019190065b915054906101000a900460f860020a0281565b610209600082603261028b565b505b50565b60005b603281101561020b5760f860020a6000826032811061022c57fe5b602091828204019190065b6101000a81548160ff021916908360f860020a900402179055508080600101915050610211565b5b50565b6106406040519081016040526032815b6000815260001990910190602001816102725790505090565b6002830191839082156103125791602002820160005b838211156102e357835183826101000a81548160ff021916908360f860020a9004021790555092602001926001016020816000010492830192600103026102a1565b80156103105782816101000a81549060ff02191690556001016020816000010492830192600103026102e3565b505b5061031e929150610322565b5090565b6101ce91905b8082111561031e57805460ff19168155600101610328565b5090565b905600a165627a7a72305820369a31471cb54acda1cc23c0c6df419a6ed121f46f2845e30057c758b8249c4d0029";
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

            var listByteArray = new List<byte>();

            for (int i = 0; i < 50; i++)
            {
                listByteArray.Add(1);
            }


            var functionInit = contract.GetFunction("initTestArrayExternally");
            receipt = await functionInit.SendTransactionAndWaitForReceiptAsync(senderAddress, new HexBigInteger(900000), null, null, listByteArray);

            result = await function.CallAsync<List<byte>>();

            for (int i = 0; i < 50; i++)
            {
                Assert.Equal(1, result[i]);
            }
        }

    }
}