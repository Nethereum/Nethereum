using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.DebugGeth.DTOs;
using Xunit;

namespace Nethereum.Tutorials
{
    public class FunctionDTO
    {
        
		[Fact]
        public async Task ShouldBeAbleToEncodeDecodeComplexInputOutput()
        {
          var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
          var password = "password";
          
          var abi = @"[{'constant':false,'inputs':[{'name':'key','type':'bytes32'},{'name':'name','type':'string'},{'name':'description','type':'string'}],'name':'StoreDocument','outputs':[{'name':'success','type':'bool'}],'type':'function'},{'constant':true,'inputs':[{'name':'','type':'bytes32'},{'name':'','type':'uint256'}],'name':'documents','outputs':[{'name':'name','type':'string'},{'name':'description','type':'string'},{'name':'sender','type':'address'}],'type':'function'}]";
          
          var byteCode = "0x6060604052610659806100126000396000f360606040526000357c0100000000000000000000000000000000000000000000000000000000900480634a75c0ff1461004457806379c17cc5146100fe57610042565b005b6100e86004808035906020019091908035906020019082018035906020019191908080601f016020809104026020016040519081016040528093929190818152602001838380828437820191505050505050909091908035906020019082018035906020019191908080601f0160208091040260200160405190810160405280939291908181526020018383808284378201915050505050509090919050506102c9565b6040518082815260200191505060405180910390f35b61011d600480803590602001909190803590602001909190505061025b565b6040518080602001806020018473ffffffffffffffffffffffffffffffffffffffff1681526020018381038352868181546001816001161561010002031660029004815260200191508054600181600116156101000203166002900480156101c65780601f1061019b576101008083540402835291602001916101c6565b820191906000526020600020905b8154815290600101906020018083116101a957829003601f168201915b50508381038252858181546001816001161561010002031660029004815260200191508054600181600116156101000203166002900480156102495780601f1061021e57610100808354040283529160200191610249565b820191906000526020600020905b81548152906001019060200180831161022c57829003601f168201915b50509550505050505060405180910390f35b600060005060205281600052604060002060005081815481101561000257906000526020600020906003020160005b9150915050806000016000509080600101600050908060020160009054906101000a900473ffffffffffffffffffffffffffffffffffffffff16905083565b60006060604051908101604052806020604051908101604052806000815260200150815260200160206040519081016040528060008152602001508152602001600081526020015060606040519081016040528085815260200184815260200133815260200150905060006000506000868152602001908152602001600020600050805480600101828181548183558181151161049257600302816003028360005260206000209182019101610491919061037f565b8082111561048d57600060008201600050805460018160011615610100020316600290046000825580601f106103b557506103f2565b601f0160209004906000526020600020908101906103f191906103d3565b808211156103ed57600081815060009055506001016103d3565b5090565b5b5060018201600050805460018160011615610100020316600290046000825580601f1061041f575061045c565b601f01602090049060005260206000209081019061045b919061043d565b80821115610457576000818150600090555060010161043d565b5090565b5b506002820160006101000a81549073ffffffffffffffffffffffffffffffffffffffff02191690555060010161037f565b5090565b5b5050509190906000526020600020906003020160005b8390919091506000820151816000016000509080519060200190828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061050357805160ff1916838001178555610534565b82800160010185558215610534579182015b82811115610533578251826000505591602001919060010190610515565b5b50905061055f9190610541565b8082111561055b5760008181506000905550600101610541565b5090565b50506020820151816001016000509080519060200190828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f106105b657805160ff19168380011785556105e7565b828001600101855582156105e7579182015b828111156105e65782518260005055916020019190600101906105c8565b5b50905061061291906105f4565b8082111561060e57600081815060009055506001016105f4565b5090565b505060408201518160020160006101000a81548173ffffffffffffffffffffffffffffffffffffffff0219169083021790555050505060019150610651565b50939250505056";

        
          
          var web3 = new Web3.Web3();
          
          var unlockResult = await web3.Personal.UnlockAccount.SendRequestAsync(senderAddress, password, new HexBigInteger(120));
          Assert.True(unlockResult);

          var transactionHash = await web3.Eth.DeployContract.SendRequestAsync(byteCode, senderAddress, new HexBigInteger(900000));
          var receipt = await MineAndGetReceiptAsync(web3, transactionHash);
          
          var contractAddress = receipt.ContractAddress;

          var contract = web3.Eth.GetContract(abi, contractAddress);

          var storeFunction = contract.GetFunction("StoreDocument");
          var documentsFunction = contract.GetFunction("documents");

        
          transactionHash = await storeFunction.SendTransactionAsync(senderAddress, new HexBigInteger(900000), null, "key1", "hello", "solidity is great");
          transactionHash = await storeFunction.SendTransactionAsync(senderAddress, new HexBigInteger(900000), null,  "key1", "hello again", "ethereum is great");
          
          receipt = await MineAndGetReceiptAsync(web3, transactionHash);
          
          var result = await documentsFunction.CallDeserializingToObjectAsync<Document>("key1", 0);
          var result2 = await documentsFunction.CallDeserializingToObjectAsync<Document>("key1", 1);
        
           Assert.Equal("hello", result.Name);
           Assert.Equal("solidity is great", result.Description);
           
           Assert.Equal("hello again", result2.Name);
           Assert.Equal("ethereum is great", result2.Description);

        }

       /*

       struct Document{
        string name;
        string decription;
        address sender;
       }

      */

        [FunctionOutput]
        public class Document
        {
            [Parameter("string", "name", 1)]
            public string Name {get; set;}

            [Parameter("string", "description", 2)]
            public string Description {get; set;}

            [Parameter("address", "sender", 3)]
            public string Sender {get; set;}
        }


        public async Task<TransactionReceipt> MineAndGetReceiptAsync(Web3.Web3 web3, string transactionHash){

          var miningResult = await web3.Miner.Start.SendRequestAsync(6);
          Assert.True(miningResult);
          
          var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);

          while(receipt == null){
              Thread.Sleep(1000);
              receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
          }
          
          miningResult = await web3.Miner.Stop.SendRequestAsync();
          Assert.True(miningResult);
          return receipt;
        }

    }
}
