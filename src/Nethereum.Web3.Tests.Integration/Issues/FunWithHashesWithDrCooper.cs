using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Web3.Tests.Integration.Issues
{
    public class FunWithHashesWithDrCooper
    {
        [Fact]
        public async void Test()
        {
            /* 
             contract Hashes{
                
                function sha3Test(string _myvalue) returns (bytes32 val){
                    return sha3(_myvalue);
                }
    
                bytes32 public myHash;
    
                function storeMyHash(bytes32 _myHash){
                    myHash = _myHash;    
                }
            }
            */

            var text = "code monkeys are great";
            var hash = "0x1c21348936d43dc62d853ff6238cff94e361f8dcee9fde6fd5fbfed9ff663150";
            var web3 = new Web3(ClientFactory.GetClient());

            var sha3Hello = Web3.Sha3(text);
            Assert.Equal(hash, "0x" + sha3Hello);

            var contractByteCode =
               "0x6060604052610154806100126000396000f360606040526000357c0100000000000000000000000000000000000000000000000000000000900480632bb49eb71461004f5780637c886096146100bd578063b6f61649146100d55761004d565b005b6100a36004808035906020019082018035906020019191908080601f0160208091040260200160405190810160405280939291908181526020018383808284378201915050505050509090919050506100fc565b604051808260001916815260200191505060405180910390f35b6100d3600480803590602001909190505061013d565b005b6100e2600480505061014b565b604051808260001916815260200191505060405180910390f35b600081604051808280519060200190808383829060006004602084601f0104600f02600301f15090500191505060405180910390209050610138565b919050565b806000600050819055505b50565b6000600050548156";

            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""_myvalue"",""type"":""string""}],""name"":""sha3Test"",""outputs"":[{""name"":""val"",""type"":""bytes32""}],""type"":""function""},{""constant"":false,""inputs"":[{""name"":""_myHash"",""type"":""bytes32""}],""name"":""storeMyHash"",""outputs"":[],""type"":""function""},{""constant"":true,""inputs"":[],""name"":""myHash"",""outputs"":[{""name"":"""",""type"":""bytes32""}],""type"":""function""}]";

           
            var gethTester = GethTesterFactory.GetLocal(web3);

            var receipt = await gethTester.DeployTestContractLocal(contractByteCode);

            //"0x350b79547251fdb18b64ec17cf3783e7d854bd30" (prev deployed contract)

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            var sha3Function = contract.GetFunction("sha3Test");
            var result = await sha3Function.CallAsync<byte[]>(text);
            Assert.Equal(hash, "0x" + result.ToHex());

            var storeMyHash = contract.GetFunction("storeMyHash");
            await gethTester.UnlockAccount();
            var gas = await storeMyHash.EstimateGasAsync(gethTester.Account, null, null, hash.HexToByteArray());
            var txn = await storeMyHash.SendTransactionAsync(gethTester.Account, gas, null, hash.HexToByteArray());
            await gethTester.GetTransactionReceipt(txn);

            var myHashFuction = contract.GetFunction("myHash");
            result = await myHashFuction.CallAsync<byte[]>();
            Assert.Equal(hash, "0x" + result.ToHex());

        }
    }
}
