using System.Collections.Generic;
using System.Numerics;
using Nethereum.Contracts;
using Nethereum.XUnitEthereumClients;
using Xunit;
using Nethereum.ABI.FunctionEncoding;
using Newtonsoft.Json.Linq;

namespace Nethereum.Accounts.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class TestDecodingDefaultValuesToPropertyOutputsAndConversion
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public TestDecodingDefaultValuesToPropertyOutputsAndConversion(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldDecodeDefaultArrayAndConvertToObjectDictionary()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestInternalDynamicArrayOfDynamicStructs.StructsSample2Deployment>()
                .SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);

            var contract = web3.Eth.GetContract("[{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple[]'}],'name':'SetPurchaseOrders','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder2','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[{'name':'id','type':'uint256'}],'name':'GetPurchaseOrder','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder3','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple[]'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'}],'name':'AddLineItems','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'name':'SetPurchaseOrder','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'inputs':[],'payable':false,'stateMutability':'nonpayable','type':'constructor'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'indexed':false,'name':'purchaseOrder','type':'tuple'}],'name':'PurchaseOrderChanged','type':'event'},{'anonymous':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'indexed':false,'name':'purchaseOrder','type':'tuple[]'}],'name':'PurchaseOrdersChanged','type':'event'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'indexed':false,'name':'purchaseOrderId','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'indexed':false,'name':'lineItem','type':'tuple[]'}],'name':'LineItemsAdded','type':'event'}]", deploymentReceipt.ContractAddress);
            var functionPO3 = contract.GetFunction("GetPurchaseOrder3");
            var result = await functionPO3.CallDecodingToDefaultAsync().ConfigureAwait(false);

            var decoded = result.ConvertToObjectDictionary();
            var purchaseOrdersDefaultResult = ((IList<object>)decoded["purchaseOrder"]);
            var purchaseOrderDefaultFirst = (Dictionary<string, object>)purchaseOrdersDefaultResult[0];

            Assert.Equal(1, (BigInteger)purchaseOrderDefaultFirst["id"]);
            Assert.Equal(1000, (BigInteger)purchaseOrderDefaultFirst["customerId"]);

            var lineItemsDefaultPo1 = (IList<object>)purchaseOrderDefaultFirst["lineItem"];
            var lineItemsDefaultPo1First = (Dictionary<string, object>)lineItemsDefaultPo1[0];

            Assert.Equal(1, (BigInteger)lineItemsDefaultPo1First["id"]);
            Assert.Equal(100, (BigInteger)lineItemsDefaultPo1First["productId"]);
            Assert.Equal(2, (BigInteger)lineItemsDefaultPo1First["quantity"]);
            Assert.Equal("hello1", lineItemsDefaultPo1First["description"]);
        }

        [Fact]
        public async void ShouldDecodeDefaultArrayAndConvertToDynamicDictionary()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestInternalDynamicArrayOfDynamicStructs.StructsSample2Deployment>()
                .SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);

            var contract = web3.Eth.GetContract("[{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple[]'}],'name':'SetPurchaseOrders','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder2','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[{'name':'id','type':'uint256'}],'name':'GetPurchaseOrder','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder3','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple[]'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'}],'name':'AddLineItems','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'name':'SetPurchaseOrder','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'inputs':[],'payable':false,'stateMutability':'nonpayable','type':'constructor'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'indexed':false,'name':'purchaseOrder','type':'tuple'}],'name':'PurchaseOrderChanged','type':'event'},{'anonymous':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'indexed':false,'name':'purchaseOrder','type':'tuple[]'}],'name':'PurchaseOrdersChanged','type':'event'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'indexed':false,'name':'purchaseOrderId','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'indexed':false,'name':'lineItem','type':'tuple[]'}],'name':'LineItemsAdded','type':'event'}]", deploymentReceipt.ContractAddress);
            var functionPO3 = contract.GetFunction("GetPurchaseOrder3");
            var result = await functionPO3.CallDecodingToDefaultAsync().ConfigureAwait(false);

            var dynamicDecoded = result.ConvertToDynamicDictionary();
            Assert.Equal(1, (BigInteger)dynamicDecoded["purchaseOrder"][0]["id"]);
            Assert.Equal(1000, (BigInteger)dynamicDecoded["purchaseOrder"][0]["customerId"]);
            Assert.Equal(1, (BigInteger)dynamicDecoded["purchaseOrder"][0]["lineItem"][0]["id"]);
            Assert.Equal(100, (BigInteger)dynamicDecoded["purchaseOrder"][0]["lineItem"][0]["productId"]);
            Assert.Equal(2, (BigInteger)dynamicDecoded["purchaseOrder"][0]["lineItem"][0]["quantity"]);
            Assert.Equal("hello1", dynamicDecoded["purchaseOrder"][0]["lineItem"][0]["description"]);
        }
        
        [Fact]
        public async void ShouldDecodeDefaultArrayAndConvertToJObject()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestInternalDynamicArrayOfDynamicStructs.StructsSample2Deployment>()
                .SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);

            var contract = web3.Eth.GetContract("[{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple[]'}],'name':'SetPurchaseOrders','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder2','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[{'name':'id','type':'uint256'}],'name':'GetPurchaseOrder','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder3','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple[]'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'}],'name':'AddLineItems','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'name':'SetPurchaseOrder','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'inputs':[],'payable':false,'stateMutability':'nonpayable','type':'constructor'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'indexed':false,'name':'purchaseOrder','type':'tuple'}],'name':'PurchaseOrderChanged','type':'event'},{'anonymous':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'indexed':false,'name':'purchaseOrder','type':'tuple[]'}],'name':'PurchaseOrdersChanged','type':'event'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'indexed':false,'name':'purchaseOrderId','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'indexed':false,'name':'lineItem','type':'tuple[]'}],'name':'LineItemsAdded','type':'event'}]", deploymentReceipt.ContractAddress);
            var functionPO3 = contract.GetFunction("GetPurchaseOrder3");
            var result = await functionPO3.CallDecodingToDefaultAsync().ConfigureAwait(false);

          
            var expected = JToken.Parse(
                @"{
  ""purchaseOrder"": [
    {
                ""id"": '1',
      ""lineItem"": [
        {
                    ""id"": '1',
          ""productId"": '100',
          ""quantity"": '2',
          ""description"": ""hello1""
        },
        {
                    ""id"": '2',
          ""productId"": '200',
          ""quantity"": '3',
          ""description"": ""hello2""
        },
        {
                    ""id"": '3',
          ""productId"": '300',
          ""quantity"": '4',
          ""description"": ""hello3""
        }
      ],
      ""customerId"": '1000'
    }
  ]
}");
            Assert.True(JObject.DeepEquals(expected, result.ConvertToJObject()));
        }
        
        [Fact]
        public async void ShouldDecodeDefaultArrayAndConvertToJObjectUsingABIStringSignature()
        {

            /*
             struct PurchaseOrder
            {
                uint256 id;
                LineItem[] lineItem;
                uint256 customerId;
            }

            struct LineItem
            {
                uint256 id;
                uint256 productId;
                uint256 quantity;
                string description;
            }*/

        var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestInternalDynamicArrayOfDynamicStructs.StructsSample2Deployment>()
                .SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);
            var abiLineItem = "tuple(uint256 id, uint256 productId, uint256 quantity, string description)";
            var abiPurchaseOrder = $"tuple(uint256 id,{abiLineItem}[] lineItem, uint256 customerId)";
            var abi = $@"
function GetPurchaseOrder3() public view returns({abiPurchaseOrder}[] purchaseOrder)";

            var contract = web3.Eth.GetContract(abi, deploymentReceipt.ContractAddress);
            var functionPO3 = contract.GetFunction("GetPurchaseOrder3");
            var result = await functionPO3.CallDecodingToDefaultAsync().ConfigureAwait(false);


            var expected = JToken.Parse(
                @"{
  ""purchaseOrder"": [
    {
                ""id"": '1',
      ""lineItem"": [
        {
                    ""id"": '1',
          ""productId"": '100',
          ""quantity"": '2',
          ""description"": ""hello1""
        },
        {
                    ""id"": '2',
          ""productId"": '200',
          ""quantity"": '3',
          ""description"": ""hello2""
        },
        {
                    ""id"": '3',
          ""productId"": '300',
          ""quantity"": '4',
          ""description"": ""hello3""
        }
      ],
      ""customerId"": '1000'
    }
  ]
}");
            Assert.True(JObject.DeepEquals(expected, result.ConvertToJObject()));
        }

        public partial class StructsSample3Deployment : StructsSample3DeploymentBase
        {
            public StructsSample3Deployment() : base(BYTECODE) { }
            public StructsSample3Deployment(string byteCode) : base(byteCode) { }
        }

        public class StructsSample3DeploymentBase : ContractDeploymentMessage
        {
            public static string BYTECODE = "60806040523480156200001157600080fd5b5060018080556103e86003557f63344455000000000000000000000000000000000000000000000000124344406004819055600580546001600160a01b0319167312890d2cce102216644c59dae5baed380d84830c17905560068054808401825560008290527ff652222313e28459528d920b65115c16c04f3efc82aaedc97be59f3f377c0d3f90810183905581548085018355810183905581548085018355810183905581548085018355810183905581549384019091559190910155620000d96200045a565b506040805160808101825260018082526064602080840191825260028486018181528651808801909752600687527f68656c6c6f310000000000000000000000000000000000000000000000000000878401526060860196875281549485018083556000929092528551600490950260008051602062001cb68339815191528101958655935160008051602062001cd68339815191528501555160008051602062001cf683398151915284015594518051949594869493620001b19360008051602062001d1683398151915290910192019062000482565b50505050620001bf6200045a565b5060408051608081018252600280825260c8602080840191825260038486019081528551808701909652600686527f68656c6c6f32000000000000000000000000000000000000000000000000000086830152606085019586528354600181018086556000959095528551600490910260008051602062001cb68339815191528101918255935160008051602062001cd6833981519152850155905160008051602062001cf683398151915284015594518051949593948694936200029a9360008051602062001d1683398151915290910192019062000482565b50505050620002a86200045a565b50604080516080810182526003815261012c602080830191825260048385018181528551808701909652600686527f68656c6c6f330000000000000000000000000000000000000000000000000000868401526060850195865260028054600181018083556000929092528651930260008051602062001cb68339815191528101938455945160008051602062001cd6833981519152860155905160008051602062001cf68339815191528501559451805194959486949293620003809360008051602062001d168339815191520192019062000482565b5050600780546001818101808455600093909352805460069092027fa66cc928b5edb82af9bd49922954155ab7b0942694bea4ce44661d9a8736c6888101928355600280549496509194509192620003fc927fa66cc928b5edb82af9bd49922954155ab7b0942694bea4ce44661d9a8736c68901919062000507565b50600282810154908201556003808301549082015560048083015490820180546001600160a01b0319166001600160a01b03909216919091179055600580830180546200044d9284019190620005a6565b5050505050505062000709565b6040518060800160405280600081526020016000815260200160008152602001606081525090565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f10620004c557805160ff1916838001178555620004f5565b82800160010185558215620004f5579182015b82811115620004f5578251825591602001919060010190620004d8565b5062000503929150620005e9565b5090565b828054828255906000526020600020906004028101928215620005985760005260206000209160040282015b8281111562000598578282600082015481600001556001820154816001015560028201548160020155600382018160030190805460018160011615610100020316600290046200058592919062000609565b5050509160040191906004019062000533565b506200050392915062000682565b828054828255906000526020600020908101928215620004f55760005260206000209182015b82811115620004f5578254825591600101919060010190620005cc565b6200060691905b80821115620005035760008155600101620005f0565b90565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f10620006445780548555620004f5565b82800160010185558215620004f557600052602060002091601f0160209004820182811115620004f5578254825591600101919060010190620005cc565b6200060691905b80821115620005035760008082556001820181905560028201819055620006b46003830182620006be565b5060040162000689565b50805460018160011615610100020316600290046000825580601f10620006e6575062000706565b601f016020900490600052602060002090810190620007069190620005e9565b50565b61159d80620007196000396000f3fe608060405234801561001057600080fd5b50600436106100625760003560e01c8063357a45ec14610067578063793ce7601461007c57806381519ba81461009a578063815c844d146100ad578063a08f28cc146100c0578063cc0b4b02146100d5575b600080fd5b61007a610075366004610db4565b6100e8565b005b6100846101ce565b6040516100919190611413565b60405180910390f35b61007a6100a8366004610d77565b6103c7565b6100846100bb366004610de9565b610432565b6100c8610616565b6040516100919190611402565b61007a6100e3366004610e07565b610839565b805160009081526020819052604080822083518155908301516002820155905b82602001515181101561019057816001018360200151828151811061012957fe5b6020908102919091018101518254600181810180865560009586529484902083516004909302019182558284015190820155604082015160028201556060820151805192939192610180926003850192019061094b565b5050600190920191506101089050565b507f9989e7b45071a3a51625f3275ab6ab355999fed98a1d147b1bd25459df69493e33836040516101c2929190611395565b60405180910390a15050565b6101d66109c9565b60076000815481106101e457fe5b90600052602060002090600602016040518060c00160405290816000820154815260200160018201805480602002602001604051908101604052809291908181526020016000905b8282101561031e5783829060005260206000209060040201604051806080016040529081600082015481526020016001820154815260200160028201548152602001600382018054600181600116156101000203166002900480601f0160208091040260200160405190810160405280929190818152602001828054600181600116156101000203166002900480156103065780601f106102db57610100808354040283529160200191610306565b820191906000526020600020905b8154815290600101906020018083116102e957829003601f168201915b5050505050815250508152602001906001019061022c565b50505050815260200160028201548152602001600382015481526020016004820160009054906101000a90046001600160a01b03166001600160a01b03166001600160a01b03168152602001600582018054806020026020016040519081016040528092919081815260200182805480156103b857602002820191906000526020600020905b8154815260200190600101908083116103a4575b50505050508152505090505b90565b60005b81518110156103f7576103ef8282815181106103e257fe5b60200260200101516100e8565b6001016103ca565b507f030bd4cd6feb982193c060be2e7179a7154e82d9a3dcb385d949f97cb1fdbeba816040516104279190611402565b60405180910390a150565b61043a6109c9565b600082815260208181526040808320815160c081018352815481526001820180548451818702810187019095528085529195929486810194939192919084015b8282101561056c5783829060005260206000209060040201604051806080016040529081600082015481526020016001820154815260200160028201548152602001600382018054600181600116156101000203166002900480601f0160208091040260200160405190810160405280929190818152602001828054600181600116156101000203166002900480156105545780601f1061052957610100808354040283529160200191610554565b820191906000526020600020905b81548152906001019060200180831161053757829003601f168201915b5050505050815250508152602001906001019061047a565b50505050815260200160028201548152602001600382015481526020016004820160009054906101000a90046001600160a01b03166001600160a01b03166001600160a01b031681526020016005820180548060200260200160405190810160405280929190818152602001828054801561060657602002820191906000526020600020905b8154815260200190600101908083116105f2575b5050505050815250509050919050565b60606007805480602002602001604051908101604052809291908181526020016000905b8282101561083057838290600052602060002090600602016040518060c00160405290816000820154815260200160018201805480602002602001604051908101604052809291908181526020016000905b8282101561077e5783829060005260206000209060040201604051806080016040529081600082015481526020016001820154815260200160028201548152602001600382018054600181600116156101000203166002900480601f0160208091040260200160405190810160405280929190818152602001828054600181600116156101000203166002900480156107665780601f1061073b57610100808354040283529160200191610766565b820191906000526020600020905b81548152906001019060200180831161074957829003601f168201915b5050505050815250508152602001906001019061068c565b50505050815260200160028201548152602001600382015481526020016004820160009054906101000a90046001600160a01b03166001600160a01b03166001600160a01b031681526020016005820180548060200260200160405190810160405280929190818152602001828054801561081857602002820191906000526020600020905b815481526020019060010190808311610804575b5050505050815250508152602001906001019061063a565b50505050905090565b60005b81518110156108cd5760008084815260200190815260200160002060010182828151811061086657fe5b60209081029190910181015182546001818101808655600095865294849020835160049093020191825582840151908201556040820151600282015560608201518051929391926108bd926003850192019061094b565b50506001909201915061083c9050565b507f13fdaebbac9da33d495b4bd32c83e33786a010730713d20c5a8ef70ca576be65338383604051610901939291906113d5565b60405180910390a17f9989e7b45071a3a51625f3275ab6ab355999fed98a1d147b1bd25459df69493e336000808581526020019081526020016000206040516101c29291906113b5565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061098c57805160ff19168380011785556109b9565b828001600101855582156109b9579182015b828111156109b957825182559160200191906001019061099e565b506109c5929150610a0b565b5090565b6040518060c001604052806000815260200160608152602001600081526020016000801916815260200160006001600160a01b03168152602001606081525090565b6103c491905b808211156109c55760008155600101610a11565b6000610a3182356114bd565b9392505050565b600082601f830112610a4957600080fd5b8135610a5c610a578261144b565b611424565b91508181835260208401935060208101905083856020840282011115610a8157600080fd5b60005b83811015610aad5781610a978882610bce565b8452506020928301929190910190600101610a84565b5050505092915050565b600082601f830112610ac857600080fd5b8135610ad6610a578261144b565b81815260209384019390925082018360005b83811015610aad5781358601610afe8882610c29565b8452506020928301929190910190600101610ae8565b600082601f830112610b2557600080fd5b8135610b33610a578261144b565b81815260209384019390925082018360005b83811015610aad5781358601610b5b8882610c29565b8452506020928301929190910190600101610b45565b600082601f830112610b8257600080fd5b8135610b90610a578261144b565b81815260209384019390925082018360005b83811015610aad5781358601610bb88882610cb0565b8452506020928301929190910190600101610ba2565b6000610a3182356103c4565b600082601f830112610beb57600080fd5b8135610bf9610a578261146c565b91508082526020830160208301858383011115610c1557600080fd5b610c208382846114eb565b50505092915050565b600060808284031215610c3b57600080fd5b610c456080611424565b90506000610c538484610bce565b8252506020610c6484848301610bce565b6020830152506040610c7884828501610bce565b604083015250606082013567ffffffffffffffff811115610c9857600080fd5b610ca484828501610bda565b60608301525092915050565b600060c08284031215610cc257600080fd5b610ccc60c0611424565b90506000610cda8484610bce565b825250602082013567ffffffffffffffff811115610cf757600080fd5b610d0384828501610ab7565b6020830152506040610d1784828501610bce565b6040830152506060610d2b84828501610bce565b6060830152506080610d3f84828501610a25565b60808301525060a082013567ffffffffffffffff811115610d5f57600080fd5b610d6b84828501610a38565b60a08301525092915050565b600060208284031215610d8957600080fd5b813567ffffffffffffffff811115610da057600080fd5b610dac84828501610b71565b949350505050565b600060208284031215610dc657600080fd5b813567ffffffffffffffff811115610ddd57600080fd5b610dac84828501610cb0565b600060208284031215610dfb57600080fd5b6000610dac8484610bce565b60008060408385031215610e1a57600080fd5b6000610e268585610bce565b925050602083013567ffffffffffffffff811115610e4357600080fd5b610e4f85828601610b14565b9150509250929050565b6000610e6583836110d3565b505060200190565b6000610a31838361119e565b6000610a3183836111f9565b6000610a318383611271565b610e9a816114da565b82525050565b610e9a816114bd565b6000610eb4826114a6565b610ebe81856114b4565b9350610ec983611494565b60005b82811015610ef457610edf868351610e59565b9550610eea82611494565b9150600101610ecc565b5093949350505050565b6000610f09826114aa565b610f1381856114b4565b9350610f1e8361149a565b60005b82811015610ef457610f3b86610f368461154d565b610e59565b9550610f46826114ae565b9150600101610f21565b6000610f5b826114a6565b610f6581856114b4565b935083602082028501610f7785611494565b60005b84811015610fae578383038852610f92838351610e6d565b9250610f9d82611494565b602098909801979150600101610f7a565b50909695505050505050565b6000610fc5826114a6565b610fcf81856114b4565b935083602082028501610fe185611494565b60005b84811015610fae578383038852610ffc838351610e6d565b925061100782611494565b602098909801979150600101610fe4565b6000611023826114aa565b61102d81856114b4565b93508360208202850161103f8561149a565b60005b84811015610fae5783830388526110598383610e79565b9250611064826114ae565b602098909801979150600101611042565b6000611080826114a6565b61108a81856114b4565b93508360208202850161109c85611494565b60005b84811015610fae5783830388526110b7838351610e85565b92506110c282611494565b60209890980197915060010161109f565b610e9a816103c4565b60006110e7826114a6565b6110f181856114b4565b93506111018185602086016114f7565b61110a81611559565b9093019392505050565b600081546001811660008114611131576001811461115757611196565b607f600283041661114281876114b4565b60ff1984168152955050602085019250611196565b6002820461116581876114b4565b95506111708561149a565b60005b8281101561118f57815488820152600190910190602001611173565b8701945050505b505092915050565b805160009060808401906111b285826110d3565b5060208301516111c560208601826110d3565b5060408301516111d860408601826110d3565b50606083015184820360608601526111f082826110dc565b95945050505050565b8054600090608084019061120c8161153a565b61121686826110d3565b505060018301546112268161153a565b61123360208701826110d3565b505060028301546112438161153a565b61125060408701826110d3565b506003840185830360608701526112678382611114565b9695505050505050565b805160009060c084019061128585826110d3565b506020830151848203602086015261129d8282610fba565b91505060408301516112b260408601826110d3565b5060608301516112c560608601826110d3565b5060808301516112d86080860182610ea0565b5060a083015184820360a08601526111f08282610ea9565b805460009060c08401906113038161153a565b61130d86826110d3565b506001840185830360208701526113248382611018565b925050600284015490506113378161153a565b61134460408701826110d3565b505060038301546113548161153a565b61136160608701826110d3565b5050600483015461137181611527565b61137e6080870182610ea0565b506005840185830360a08701526112678382610efe565b604081016113a38285610e91565b8181036020830152610dac8184611271565b604081016113c38285610e91565b8181036020830152610dac81846112f0565b606081016113e38286610e91565b6113f060208301856110d3565b81810360408301526111f08184610f50565b60208082528101610a318184611075565b60208082528101610a318184611271565b60405181810167ffffffffffffffff8111828210171561144357600080fd5b604052919050565b600067ffffffffffffffff82111561146257600080fd5b5060209081020190565b600067ffffffffffffffff82111561148357600080fd5b506020601f91909101601f19160190565b60200190565b60009081526020902090565b5190565b5490565b60010190565b90815260200190565b60006114c8826114ce565b92915050565b6001600160a01b031690565b60006114c88260006114c8826114bd565b82818337506000910152565b60005b838110156115125781810151838201526020016114fa565b83811115611521576000848401525b50505050565b60006114c8611535836103c4565b6114ce565b60006114c8611548836103c4565b6103c4565b60006114c8825461153a565b601f01601f19169056fea265627a7a7230582067491039c4738f74dd4c2305eed783b636a17b2ab15fa7a656497dbf2b38288a6c6578706572696d656e74616cf50037405787fa12a823e0f2b7631cc41b3ba8828b3321ca811111fa75cd3aa3bb5ace405787fa12a823e0f2b7631cc41b3ba8828b3321ca811111fa75cd3aa3bb5acf405787fa12a823e0f2b7631cc41b3ba8828b3321ca811111fa75cd3aa3bb5ad0405787fa12a823e0f2b7631cc41b3ba8828b3321ca811111fa75cd3aa3bb5ad1";
            public StructsSample3DeploymentBase() : base(BYTECODE) { }
            public StructsSample3DeploymentBase(string byteCode) : base(byteCode) { }

        }

        

        [Fact]
        public async void ShouldDecodeDefaultArrayAndConvertToJObject2()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<StructsSample3Deployment>()
                .SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);

            var contract = web3.Eth.GetContract("[{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'},{'name':'id2','type':'bytes32'},{'name':'id3','type':'address'},{'name':'id5','type':'bytes32[]'}],'name':'purchaseOrder','type':'tuple'}],'name':'SetPurchaseOrder','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder2','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'},{'name':'id2','type':'bytes32'},{'name':'id3','type':'address'},{'name':'id5','type':'bytes32[]'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'},{'name':'id2','type':'bytes32'},{'name':'id3','type':'address'},{'name':'id5','type':'bytes32[]'}],'name':'purchaseOrder','type':'tuple[]'}],'name':'SetPurchaseOrders','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':true,'inputs':[{'name':'id','type':'uint256'}],'name':'GetPurchaseOrder','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'},{'name':'id2','type':'bytes32'},{'name':'id3','type':'address'},{'name':'id5','type':'bytes32[]'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder3','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'},{'name':'id2','type':'bytes32'},{'name':'id3','type':'address'},{'name':'id5','type':'bytes32[]'}],'name':'purchaseOrder','type':'tuple[]'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'}],'name':'AddLineItems','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'inputs':[],'payable':false,'stateMutability':'nonpayable','type':'constructor'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'},{'name':'id2','type':'bytes32'},{'name':'id3','type':'address'},{'name':'id5','type':'bytes32[]'}],'indexed':false,'name':'purchaseOrder','type':'tuple'}],'name':'PurchaseOrderChanged','type':'event'},{'anonymous':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'},{'name':'id2','type':'bytes32'},{'name':'id3','type':'address'},{'name':'id5','type':'bytes32[]'}],'indexed':false,'name':'purchaseOrder','type':'tuple[]'}],'name':'PurchaseOrdersChanged','type':'event'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'indexed':false,'name':'purchaseOrderId','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'indexed':false,'name':'lineItem','type':'tuple[]'}],'name':'LineItemsAdded','type':'event'}]", deploymentReceipt.ContractAddress);
            var functionPO3 = contract.GetFunction("GetPurchaseOrder3");
            var result = await functionPO3.CallDecodingToDefaultAsync().ConfigureAwait(false);


            var expected = JToken.Parse(
                @"{
  'purchaseOrder': [
    {
      'id': '1',
      'lineItem': [
        {
          'id': '1',
          'productId': '100',
          'quantity': '2',
          'description': 'hello1'
        },
        {
          'id': '2',
          'productId': '200',
          'quantity': '3',
          'description': 'hello2'
        },
        {
          'id': '3',
          'productId': '300',
          'quantity': '4',
          'description': 'hello3'
        }
      ],
      'customerId': '1000',
      'id2': '6334445500000000000000000000000000000000000000000000000012434440',
      'id3': '0x12890D2cce102216644c59daE5baed380d84830c',
      'id5': [
        '6334445500000000000000000000000000000000000000000000000012434440',
        '6334445500000000000000000000000000000000000000000000000012434440',
        '6334445500000000000000000000000000000000000000000000000012434440',
        '6334445500000000000000000000000000000000000000000000000012434440',
        '6334445500000000000000000000000000000000000000000000000012434440'
      ]
    }
  ]
}");
            Assert.True(JObject.DeepEquals(expected, result.ConvertToJObject()));
        }
    }

    /*

    pragma solidity "0.5.7";
pragma experimental ABIEncoderV2;

contract StructsSample3
    {
        mapping(uint => PurchaseOrder) purchaseOrders;
        PurchaseOrder po;
        PurchaseOrder []
        purchaseOrders2;
        event PurchaseOrderChanged(address sender, PurchaseOrder purchaseOrder);
        event PurchaseOrdersChanged(PurchaseOrder []
        purchaseOrder);
        event LineItemsAdded(address sender, uint purchaseOrderId, LineItem []
        lineItem);

        constructor() public {
 
            po.id = 1 ;
            po.customerId = 1000;
            po.id2 = 0x6334445500000000000000000000000000000000000000000000000012434440;
            po.id3 = 0x12890D2cce102216644c59daE5baed380d84830c;
            po.id5.push(po.id2);
            po.id5.push(po.id2);
            po.id5.push(po.id2);
            po.id5.push(po.id2);
            po.id5.push(po.id2);
            LineItem memory lineItem = LineItem(1,100,2, "hello1");
    po.lineItem.push(lineItem);
           
            
            LineItem memory lineItem2 = LineItem(2,200,3, "hello2");
    po.lineItem.push(lineItem2);
            
             LineItem memory lineItem3 = LineItem(3,300,4, "hello3");
    po.lineItem.push(lineItem3);
            purchaseOrders2.push(po);
           
           
        }

struct PurchaseOrder
{
    uint256 id;
    LineItem[] lineItem;
    uint256 customerId;
    bytes32 id2;
    address id3;
    bytes32[] id5;
}

struct LineItem
{
    uint256 id;
    uint256 productId;
    uint256 quantity;
    string description;
}

struct Test
{
    uint256 id;
    string[] strings;
}

function SetPurchaseOrder(PurchaseOrder memory purchaseOrder) public
{
    PurchaseOrder storage purchaseOrderTemp = purchaseOrders[purchaseOrder.id];
    purchaseOrderTemp.id = purchaseOrder.id;
    purchaseOrderTemp.customerId = purchaseOrder.customerId;


    for (uint x = 0; x < purchaseOrder.lineItem.length; x++)
    {
        purchaseOrderTemp.lineItem.push(purchaseOrder.lineItem[x]);
    }

    emit PurchaseOrderChanged(msg.sender, purchaseOrder);
}

function SetPurchaseOrders(PurchaseOrder[] memory purchaseOrder) public
{
    for (uint i = 0; i < purchaseOrder.length; i++)
    {
        SetPurchaseOrder(purchaseOrder[i]);
    }
    emit PurchaseOrdersChanged(purchaseOrder);
}

function GetPurchaseOrder(uint id) view public returns (PurchaseOrder memory purchaseOrder)
{
    return purchaseOrders[id];
}

function GetPurchaseOrder2() public view returns(PurchaseOrder memory purchaseOrder)
{
    return purchaseOrders2[0];
}

function GetPurchaseOrder3() public view returns(PurchaseOrder[] memory purchaseOrder)
{
    return purchaseOrders2;
}

function AddLineItems(uint id, LineItem[] memory lineItem) public
{
    for (uint x = 0; x < lineItem.length; x++)
    {
        purchaseOrders[id].lineItem.push(lineItem[x]);
    }
    emit LineItemsAdded(msg.sender, id, lineItem);
    emit PurchaseOrderChanged(msg.sender, purchaseOrders[id]);
}
        
}
*/
}