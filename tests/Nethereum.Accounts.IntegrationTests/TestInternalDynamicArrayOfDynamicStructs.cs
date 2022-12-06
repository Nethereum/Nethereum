using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Web3.Accounts;
using Nethereum.XUnitEthereumClients;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using System.Collections;
using System.Text;
using System.Diagnostics;
using System;
using Newtonsoft.Json.Linq;
using Nethereum.Hex.HexTypes;
using System.Linq;


namespace Nethereum.Accounts.IntegrationTests
{

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class TestInternalDynamicArrayOfDynamicStructs
    {

        //Struct with dynamic array of structs containing strings

        /*
pragma solidity "0.5.7";
pragma experimental ABIEncoderV2;

contract StructsSample2
{
        mapping(uint => PurchaseOrder) purchaseOrders;
        PurchaseOrder po;
        PurchaseOrder[] purchaseOrders2;
        event PurchaseOrderChanged(address sender, PurchaseOrder purchaseOrder);
        event PurchaseOrdersChanged(PurchaseOrder[] purchaseOrder);
        event LineItemsAdded(address sender, uint purchaseOrderId, LineItem[] lineItem);
        
        constructor() public {
 
            po.id = 1 ;
            po.customerId = 1000;
            LineItem memory lineItem = LineItem(1,100,2, "hello1");
            po.lineItem.push(lineItem);
           
            
            LineItem memory lineItem2 = LineItem(2,200,3, "hello2");
            po.lineItem.push(lineItem2);
            
             LineItem memory lineItem3 = LineItem(3,300,4, "hello3");
            po.lineItem.push(lineItem3);
            purchaseOrders2.push(po);
           
           
        }
        
        struct PurchaseOrder {
            uint256 id;
            LineItem[] lineItem;
            uint256 customerId;
        }

        struct LineItem {
            uint256 id;
            uint256 productId;
            uint256 quantity;
            string description;
        }
        
        struct Test{
            uint256 id;
            string[] strings;    
        }

        function SetPurchaseOrder(PurchaseOrder memory purchaseOrder) public {
            PurchaseOrder storage purchaseOrderTemp = purchaseOrders[purchaseOrder.id];
            purchaseOrderTemp.id = purchaseOrder.id;
            purchaseOrderTemp.customerId = purchaseOrder.customerId;
            
          
            for (uint x = 0; x < purchaseOrder.lineItem.length; x++)
            {
                purchaseOrderTemp.lineItem.push(purchaseOrder.lineItem[x]);
            }
            
            emit PurchaseOrderChanged(msg.sender, purchaseOrder);
        }

        function SetPurchaseOrders(PurchaseOrder[] memory purchaseOrder) public {
            for (uint i = 0; i < purchaseOrder.length; i ++)
            {
                SetPurchaseOrder(purchaseOrder[i]);
            }
             emit PurchaseOrdersChanged(purchaseOrder);
        }

        function GetPurchaseOrder(uint id) view public returns (PurchaseOrder memory purchaseOrder) {
           return purchaseOrders[id];
        }

        function GetPurchaseOrder2() public view returns (PurchaseOrder memory purchaseOrder) {
           return purchaseOrders2[0];
        }
        
        function GetPurchaseOrder3() public view returns (PurchaseOrder[] memory purchaseOrder) {
            return purchaseOrders2;
        }
        
        function AddLineItems(uint id, LineItem[] memory lineItem) public {
            for (uint x = 0; x < lineItem.length; x++)
            {
                purchaseOrders[id].lineItem.push(lineItem[x]);
            }
            emit LineItemsAdded(msg.sender, id, lineItem);
            emit PurchaseOrderChanged(msg.sender, purchaseOrders[id]);
        }
        
}

*/

    private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public TestInternalDynamicArrayOfDynamicStructs(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public void ShouldEncodeSignatureWithStructArrays()
        {
            var functionAbi = ABITypedRegistry.GetFunctionABI<SetPurchaseOrderFunction>();
            Assert.Equal("f79eb4a2", functionAbi.Sha3Signature);

            functionAbi = ABITypedRegistry.GetFunctionABI<SetPurchaseOrdersFunction>();
            Assert.Equal("1a9542af", functionAbi.Sha3Signature);
        }

        [Fact]
        public void ShouldEncodeStructContainingStructArrayWithDynamicTuple()
        {
            var purchaseOrder = new PurchaseOrder();
            purchaseOrder.CustomerId = 2;
            purchaseOrder.Id = 1;
            purchaseOrder.LineItem = new List<LineItem>();
            purchaseOrder.LineItem.Add(new LineItem() { Id = 1, ProductId = 2, Quantity = 3, Description = "hello" });

            var func = new SetPurchaseOrderFunction() { PurchaseOrder = purchaseOrder };
            var data = func.GetCallData();
            var expected = "0000000000000000000000000000000000000000000000000000000000000020000000000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000600000000000000000000000000000000000000000000000000000000000000002000000000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000200000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000000200000000000000000000000000000000000000000000000000000000000000030000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000000568656c6c6f000000000000000000000000000000000000000000000000000000";
            Assert.Equal(expected, data.ToHex().Substring(8));
        }

        /*
        constructor() public {

          po.id = 1 ;
          po.customerId = 1000;
          LineItem memory lineItem = LineItem(1,100,2, "hello1");
          po.lineItem.push(lineItem);


          LineItem memory lineItem2 = LineItem(2,200,3, "hello2");
          po.lineItem.push(lineItem2);
      }
      */

        [Fact]
        public void ShouldEncodeStructContainingStructArrayWithDynamicTuple2()
        {
            var purchaseOrder = new PurchaseOrder();
            purchaseOrder.CustomerId = 1000;
            purchaseOrder.Id = 1;
            purchaseOrder.LineItem = new List<LineItem>();
            purchaseOrder.LineItem.Add(new LineItem() { Id = 1, ProductId = 100, Quantity = 2, Description = "hello1" });
            purchaseOrder.LineItem.Add(new LineItem() { Id = 2, ProductId = 200, Quantity = 3, Description = "hello2" });

            var func = new SetPurchaseOrderFunction() { PurchaseOrder = purchaseOrder };
            var data = func.GetCallData();
            var expected = "00000000000000000000000000000000000000000000000000000000000000200000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000006000000000000000000000000000000000000000000000000000000000000003e80000000000000000000000000000000000000000000000000000000000000002000000000000000000000000000000000000000000000000000000000000004000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000006400000000000000000000000000000000000000000000000000000000000000020000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000000668656c6c6f310000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000200000000000000000000000000000000000000000000000000000000000000c800000000000000000000000000000000000000000000000000000000000000030000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000000668656c6c6f320000000000000000000000000000000000000000000000000000";
            Assert.Equal(expected, data.ToHex().Substring(8));
        }

        /*
        constructor() public {

          po.id = 1 ;
          po.customerId = 1000;
          LineItem memory lineItem = LineItem(1,100,2, "hello1");
          po.lineItem.push(lineItem);


          LineItem memory lineItem2 = LineItem(2,200,3, "hello2");
          po.lineItem.push(lineItem2);

          LineItem memory lineItem3 = LineItem(3,300,4, "hello3");
          po.lineItem.push(lineItem3);
          purchaseOrders2.push(po);


      }
      */

        [Fact]
        public void ShouldEncodeStructContainingStructArrayWithDynamicTuple3()
        {
            var purchaseOrder = new PurchaseOrder();
            purchaseOrder.CustomerId = 1000;
            purchaseOrder.Id = 1;
            purchaseOrder.LineItem = new List<LineItem>();
            purchaseOrder.LineItem.Add(new LineItem() { Id = 1, ProductId = 100, Quantity = 2, Description = "hello1" });
            purchaseOrder.LineItem.Add(new LineItem() { Id = 2, ProductId = 200, Quantity = 3, Description = "hello2" });
            purchaseOrder.LineItem.Add(new LineItem() { Id = 3, ProductId = 300, Quantity = 4, Description = "hello3" });


            var func = new SetPurchaseOrderFunction() { PurchaseOrder = purchaseOrder };
            var data = func.GetCallData();
            var expected = "00000000000000000000000000000000000000000000000000000000000000200000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000006000000000000000000000000000000000000000000000000000000000000003e800000000000000000000000000000000000000000000000000000000000000030000000000000000000000000000000000000000000000000000000000000060000000000000000000000000000000000000000000000000000000000000012000000000000000000000000000000000000000000000000000000000000001e00000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000006400000000000000000000000000000000000000000000000000000000000000020000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000000668656c6c6f310000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000200000000000000000000000000000000000000000000000000000000000000c800000000000000000000000000000000000000000000000000000000000000030000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000000668656c6c6f3200000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000003000000000000000000000000000000000000000000000000000000000000012c00000000000000000000000000000000000000000000000000000000000000040000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000000668656c6c6f330000000000000000000000000000000000000000000000000000";
            Assert.Equal(expected, data.ToHex().Substring(8));
        }


        [Fact]
        public async void ShouldEncodeStructContainingArrayUsingJson()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<StructsSample2Deployment>()
                .SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);

            var contract = web3.Eth.GetContract("[{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple[]'}],'name':'SetPurchaseOrders','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder2','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[{'name':'id','type':'uint256'}],'name':'GetPurchaseOrder','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder3','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple[]'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'}],'name':'AddLineItems','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'name':'SetPurchaseOrder','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'inputs':[],'payable':false,'stateMutability':'nonpayable','type':'constructor'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'indexed':false,'name':'purchaseOrder','type':'tuple'}],'name':'PurchaseOrderChanged','type':'event'},{'anonymous':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'indexed':false,'name':'purchaseOrder','type':'tuple[]'}],'name':'PurchaseOrdersChanged','type':'event'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'indexed':false,'name':'purchaseOrderId','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'indexed':false,'name':'lineItem','type':'tuple[]'}],'name':'LineItemsAdded','type':'event'}]", deploymentReceipt.ContractAddress);

            var json = @"{'purchaseOrder':{
  'id': 1,
  'lineItem': [
    {
      'id': 1,
      'productId': 100,
      'quantity': 2,
      'description': 'hello1'
    },
    {
      'id': 2,
      'productId': 200,
      'quantity': 3,
      'description': 'hello2'
    }],
    'customerId': 1000
}}";
            
            var functionPurchaseOrder = contract.GetFunction("SetPurchaseOrder");
            var values = functionPurchaseOrder.ConvertJsonToObjectInputParameters(json);
            var receiptSending = await functionPurchaseOrder.
                                           SendTransactionAndWaitForReceiptAsync(
                                               EthereumClientIntegrationFixture.AccountAddress,
                                               new HexBigInteger(900000), null, null,
                                                values.ToArray()).ConfigureAwait(false);


            var eventPurchaseOrder = contract.GetEvent("PurchaseOrderChanged");
            var eventOutputs = eventPurchaseOrder.DecodeAllEventsDefaultForEvent(receiptSending.Logs);

            var jObjectEvent = eventOutputs[0].Event.ConvertToJObject();

            var expectedJObject = JObject.Parse(@"{
  'sender': '0x12890D2cce102216644c59daE5baed380d84830c',
  'purchaseOrder':{
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
    }],
    'customerId': '1000'
    }
}");
            Assert.True(JObject.DeepEquals(expectedJObject, jObjectEvent));
        }

        

        [Fact]
        public async void ShouldEncodeDecodeStructContainingStructsArrayOnlyUsingObjects()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<StructsSample2Deployment>()
                .SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);

            var contract = web3.Eth.GetContract("[{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple[]'}],'name':'SetPurchaseOrders','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder2','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[{'name':'id','type':'uint256'}],'name':'GetPurchaseOrder','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder3','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple[]'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'}],'name':'AddLineItems','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'name':'SetPurchaseOrder','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'inputs':[],'payable':false,'stateMutability':'nonpayable','type':'constructor'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'indexed':false,'name':'purchaseOrder','type':'tuple'}],'name':'PurchaseOrderChanged','type':'event'},{'anonymous':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'indexed':false,'name':'purchaseOrder','type':'tuple[]'}],'name':'PurchaseOrdersChanged','type':'event'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'indexed':false,'name':'purchaseOrderId','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'indexed':false,'name':'lineItem','type':'tuple[]'}],'name':'LineItemsAdded','type':'event'}]", deploymentReceipt.ContractAddress);

            /*
              struct PurchaseOrder {
            uint256 id;
            LineItem[] lineItem;
            uint256 customerId;
        }

        struct LineItem {
            uint256 id;
            uint256 productId;
            uint256 quantity;
            string description;
        }
         
             */
            var purchaseOrder = new List<object>();
            purchaseOrder.Add(1); // id
            
            var lineItem1 = new List<object>();
            lineItem1.Add(1); //id
            lineItem1.Add(100); //productId
            lineItem1.Add(2); //quantity
            lineItem1.Add("hello1"); //description

            var lineItem2 = new List<object>();
            lineItem2.Add(2); //id
            lineItem2.Add(200); //productId
            lineItem2.Add(3); //quantity
            lineItem2.Add("hello2"); //description

            var lineItems = new List<object>();
            lineItems.Add(lineItem1.ToArray());
            lineItems.Add(lineItem2.ToArray());

            purchaseOrder.Add(lineItems); // lineItems

            purchaseOrder.Add(1000); // customerId



            var functionPurchaseOrder = contract.GetFunction("SetPurchaseOrder");
            var receiptSending = await functionPurchaseOrder.
                                            SendTransactionAndWaitForReceiptAsync(
                                                EthereumClientIntegrationFixture.AccountAddress, 
                                                new HexBigInteger(900000), null, null,
                                                new object[] { purchaseOrder.ToArray() }).ConfigureAwait(false);


            var eventPurchaseOrder = contract.GetEvent<PurchaseOrderChangedEventDTO>();
            var eventOutputs = eventPurchaseOrder.DecodeAllEventsForEvent(receiptSending.Logs);
            var purchaseOrderResult = eventOutputs[0].Event.PurchaseOrder;
      
            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);
            Assert.Equal("hello1", purchaseOrderResult.LineItem[0].Description);

            Assert.Equal(2, purchaseOrderResult.LineItem[1].Id);
            Assert.Equal(200, purchaseOrderResult.LineItem[1].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[1].Quantity);
            Assert.Equal("hello2", purchaseOrderResult.LineItem[1].Description);
           
        }


        [Fact]
        public async void ShouldEncodeDecodeStructContainingStructsArrayOnlyUsingTypedStructs()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<StructsSample2Deployment>()
                .SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);

            var contract = web3.Eth.GetContract("[{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple[]'}],'name':'SetPurchaseOrders','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder2','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[{'name':'id','type':'uint256'}],'name':'GetPurchaseOrder','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':true,'inputs':[],'name':'GetPurchaseOrder3','outputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple[]'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'}],'name':'AddLineItems','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'constant':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'name':'purchaseOrder','type':'tuple'}],'name':'SetPurchaseOrder','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'},{'inputs':[],'payable':false,'stateMutability':'nonpayable','type':'constructor'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'indexed':false,'name':'purchaseOrder','type':'tuple'}],'name':'PurchaseOrderChanged','type':'event'},{'anonymous':false,'inputs':[{'components':[{'name':'id','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'name':'lineItem','type':'tuple[]'},{'name':'customerId','type':'uint256'}],'indexed':false,'name':'purchaseOrder','type':'tuple[]'}],'name':'PurchaseOrdersChanged','type':'event'},{'anonymous':false,'inputs':[{'indexed':false,'name':'sender','type':'address'},{'indexed':false,'name':'purchaseOrderId','type':'uint256'},{'components':[{'name':'id','type':'uint256'},{'name':'productId','type':'uint256'},{'name':'quantity','type':'uint256'},{'name':'description','type':'string'}],'indexed':false,'name':'lineItem','type':'tuple[]'}],'name':'LineItemsAdded','type':'event'}]", deploymentReceipt.ContractAddress);

            var purchaseOrder = new PurchaseOrder();
            purchaseOrder.CustomerId = 1000;
            purchaseOrder.Id = 1;
            purchaseOrder.LineItem = new List<LineItem>();
            purchaseOrder.LineItem.Add(new LineItem() { Id = 1, ProductId = 100, Quantity = 2, Description = "hello1" });
            purchaseOrder.LineItem.Add(new LineItem() { Id = 2, ProductId = 200, Quantity = 3, Description = "hello2" });

            var functionPurchaseOrder = contract.GetFunction("SetPurchaseOrder");
            var receiptSending = await functionPurchaseOrder.SendTransactionAndWaitForReceiptAsync(EthereumClientIntegrationFixture.AccountAddress, new HexBigInteger(900000), null, null,
                purchaseOrder).ConfigureAwait(false);
            
            
            var eventPurchaseOrder = contract.GetEvent<PurchaseOrderChangedEventDTO>();
            var eventOutputs = eventPurchaseOrder.DecodeAllEventsForEvent(receiptSending.Logs);
            var purchaseOrderResult = eventOutputs[0].Event.PurchaseOrder;
            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);
            Assert.Equal("hello1", purchaseOrderResult.LineItem[0].Description);

            Assert.Equal(2, purchaseOrderResult.LineItem[1].Id);
            Assert.Equal(200, purchaseOrderResult.LineItem[1].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[1].Quantity);
            Assert.Equal("hello2", purchaseOrderResult.LineItem[1].Description);


            var lineItems = new List<LineItem>();
            lineItems.Add(new LineItem() { Id = 3, ProductId = 300, Quantity = 2, Description = "hello3" });
            lineItems.Add(new LineItem() { Id = 4, ProductId = 400, Quantity = 3, Description = "hello4" });

            var addLineItemsUntypedFunction = contract.GetFunction("AddLineItems");
            
            receiptSending = await addLineItemsUntypedFunction.SendTransactionAndWaitForReceiptAsync(EthereumClientIntegrationFixture.AccountAddress, new HexBigInteger(900000), null,
                null, 1, lineItems);

            var eventLineItemsAdded = contract.GetEvent("LineItemsAdded");

            var x = eventLineItemsAdded.DecodeAllEventsDefaultForEvent(receiptSending.Logs);

            var expectedJObject = JObject.Parse(@"{
  'sender': '0x12890D2cce102216644c59daE5baed380d84830c',
  'purchaseOrderId': '1',
  'lineItem': [
    {
      'id': '3',
      'productId': '300',
      'quantity': '2',
      'description': 'hello3'
    },
    {
      'id': '4',
      'productId': '400',
      'quantity': '3',
      'description': 'hello4'
    }
  ]
}");
            Assert.True(JObject.DeepEquals(expectedJObject, x[0].Event.ConvertToJObject()));

            Assert.True(JToken.DeepEquals(expectedJObject,
                ABITypedRegistry.GetEvent<LineItemsAddedEventDTO>()
                                .DecodeEventDefaultTopics(receiptSending.Logs[0])
                                .Event.ConvertToJObject()));

            var getPurchaseOrderFunction = contract.GetFunction("GetPurchaseOrder");
            //Not deserialising to DTO so just simple CallAsync
            purchaseOrderResult = await getPurchaseOrderFunction.CallAsync<PurchaseOrder>(1).ConfigureAwait(false);

            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);
            Assert.Equal(2, purchaseOrderResult.LineItem[1].Id);
            Assert.Equal(200, purchaseOrderResult.LineItem[1].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[1].Quantity);
            Assert.Equal(3, purchaseOrderResult.LineItem[2].Id);
            Assert.Equal(300, purchaseOrderResult.LineItem[2].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[2].Quantity);
            Assert.Equal(4, purchaseOrderResult.LineItem[3].Id);
            Assert.Equal(400, purchaseOrderResult.LineItem[3].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[3].Quantity);


            var purchaseOrderResultOutput = await getPurchaseOrderFunction.CallAsync<GetPurchaseOrderOutputDTO>(1).ConfigureAwait(false);

            purchaseOrderResult = purchaseOrderResultOutput.PurchaseOrder;

            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);
            Assert.Equal(2, purchaseOrderResult.LineItem[1].Id);
            Assert.Equal(200, purchaseOrderResult.LineItem[1].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[1].Quantity);
            Assert.Equal(3, purchaseOrderResult.LineItem[2].Id);
            Assert.Equal(300, purchaseOrderResult.LineItem[2].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[2].Quantity);
            Assert.Equal(4, purchaseOrderResult.LineItem[3].Id);
            Assert.Equal(400, purchaseOrderResult.LineItem[3].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[3].Quantity);


            var listPurchaseOrder = new List<PurchaseOrder>();
            listPurchaseOrder.Add(purchaseOrder);

            var setPurchaseOrdersFunction = contract.GetFunction("SetPurchaseOrders");

            receiptSending = await setPurchaseOrdersFunction.SendTransactionAndWaitForReceiptAsync(EthereumClientIntegrationFixture.AccountAddress, new HexBigInteger(900000), null,
                null, listPurchaseOrder);
            

            var eventPurchaseOrders = contract.GetEvent("PurchaseOrdersChanged");
            var eventPurchaseOrdersOutputs = eventPurchaseOrders.DecodeAllEventsForEvent<PurchaseOrdersChangedEventDTO>(receiptSending.Logs);
            purchaseOrderResult = eventPurchaseOrdersOutputs[0].Event.PurchaseOrder[0];

            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);
            Assert.Equal(2, purchaseOrderResult.LineItem[1].Id);
            Assert.Equal(200, purchaseOrderResult.LineItem[1].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[1].Quantity);


            //Stored array on constructor
            var getPurchaseOrder3Function = contract.GetFunction("GetPurchaseOrder3");

            //Not deserialising to DTO so just simple CallAsync
            var purchaseOrderResults = await getPurchaseOrder3Function.CallAsync<List<PurchaseOrder>>();
            /*
              constructor() public {
            _purchaseOrder.id = 1;
            _purchaseOrder.customerId = 2;
            LineItem memory lineItem = LineItem(1,2,3);
            _purchaseOrder.lineItem.push(lineItem);
            purchaseOrdersArray.push(_purchaseOrder); 
        }
        */
            purchaseOrderResult = purchaseOrderResults[0];
            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);
        }

        [Fact]
        public async void ShouldEncodeDecodeStructContainingStructsArray()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<StructsSample2Deployment>()
                .SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);

            var purchaseOrder = new PurchaseOrder();
            purchaseOrder.CustomerId = 1000;
            purchaseOrder.Id = 1;
            purchaseOrder.LineItem = new List<LineItem>();
            purchaseOrder.LineItem.Add(new LineItem() { Id = 1, ProductId = 100, Quantity = 2, Description = "hello1" });
            purchaseOrder.LineItem.Add(new LineItem() { Id = 2, ProductId = 200, Quantity = 3, Description = "hello2"});

            var contractHandler = web3.Eth.GetContractHandler(deploymentReceipt.ContractAddress);

            var receiptSending = await contractHandler.SendRequestAndWaitForReceiptAsync(new SetPurchaseOrderFunction() { PurchaseOrder = purchaseOrder }).ConfigureAwait(false);
            var eventPurchaseOrder = contractHandler.GetEvent<PurchaseOrderChangedEventDTO>();
            var eventOutputs = eventPurchaseOrder.DecodeAllEventsForEvent(receiptSending.Logs);
            var purchaseOrderResult = eventOutputs[0].Event.PurchaseOrder;
            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);
            Assert.Equal("hello1", purchaseOrderResult.LineItem[0].Description);

            Assert.Equal(2, purchaseOrderResult.LineItem[1].Id);
            Assert.Equal(200, purchaseOrderResult.LineItem[1].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[1].Quantity);
            Assert.Equal("hello2", purchaseOrderResult.LineItem[1].Description);


            var query = await contractHandler.QueryDeserializingToObjectAsync<GetPurchaseOrderFunction, GetPurchaseOrderOutputDTO>(new GetPurchaseOrderFunction() { Id = 1 }).ConfigureAwait(false);

            purchaseOrderResult = query.PurchaseOrder;
            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);
            Assert.Equal("hello1", purchaseOrderResult.LineItem[0].Description);
            Assert.Equal(2, purchaseOrderResult.LineItem[1].Id);
            Assert.Equal(200, purchaseOrderResult.LineItem[1].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[1].Quantity);
            Assert.Equal("hello2", purchaseOrderResult.LineItem[1].Description);
       
            var lineItems = new List<LineItem>();
            lineItems.Add(new LineItem() { Id = 3, ProductId = 300, Quantity = 2, Description = "hello3" });
            lineItems.Add(new LineItem() { Id = 4, ProductId = 400, Quantity = 3, Description = "hello4" });

            var lineItemsFunction = new AddLineItemsFunction() { Id = 1, LineItem = lineItems };
            var data = lineItemsFunction.GetCallData().ToHex();

            
            receiptSending = await contractHandler.SendRequestAndWaitForReceiptAsync(new AddLineItemsFunction() { Id = 1, LineItem = lineItems }).ConfigureAwait(false);

            var lineItemsEvent = contractHandler.GetEvent<LineItemsAddedEventDTO>();
            var lineItemsLogs = lineItemsEvent.DecodeAllEventsForEvent(receiptSending.Logs);


            

            query = await contractHandler.QueryDeserializingToObjectAsync<GetPurchaseOrderFunction, GetPurchaseOrderOutputDTO>(new GetPurchaseOrderFunction() { Id = 1 }).ConfigureAwait(false);
            purchaseOrderResult = query.PurchaseOrder;
            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);
            Assert.Equal(2, purchaseOrderResult.LineItem[1].Id);
            Assert.Equal(200, purchaseOrderResult.LineItem[1].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[1].Quantity);
            Assert.Equal(3, purchaseOrderResult.LineItem[2].Id);
            Assert.Equal(300, purchaseOrderResult.LineItem[2].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[2].Quantity);
            Assert.Equal(4, purchaseOrderResult.LineItem[3].Id);
            Assert.Equal(400, purchaseOrderResult.LineItem[3].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[3].Quantity);



            //Purchase Orders

            var listPurchaseOrder = new List<PurchaseOrder>();
            listPurchaseOrder.Add(purchaseOrder);
            var func = new SetPurchaseOrdersFunction() { PurchaseOrder = listPurchaseOrder };
            receiptSending = await contractHandler.SendRequestAndWaitForReceiptAsync(func).ConfigureAwait(false);
            var eventPurchaseOrders = contractHandler.GetEvent<PurchaseOrdersChangedEventDTO>();
            var eventPurchaseOrdersOutputs = eventPurchaseOrders.DecodeAllEventsForEvent(receiptSending.Logs);
            purchaseOrderResult = eventPurchaseOrdersOutputs[0].Event.PurchaseOrder[0];

            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);
            Assert.Equal(2, purchaseOrderResult.LineItem[1].Id);
            Assert.Equal(200, purchaseOrderResult.LineItem[1].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[1].Quantity);

            //Stored array on constructor
            var query2 = await contractHandler.QueryDeserializingToObjectAsync<GetPurchaseOrder3Function, GetPurchaseOrder3OutputDTO>().ConfigureAwait(false);
            /*
              constructor() public {
            _purchaseOrder.id = 1;
            _purchaseOrder.customerId = 2;
            LineItem memory lineItem = LineItem(1,2,3);
            _purchaseOrder.lineItem.push(lineItem);
            purchaseOrdersArray.push(_purchaseOrder); 
        }
        */

        purchaseOrderResult = query2.PurchaseOrder[0];
            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);

           
        }

        

        public partial class PurchaseOrder : PurchaseOrderBase { }

        public class PurchaseOrderBase
        {
            [Parameter("uint256", "id", 1)]
            public virtual BigInteger Id { get; set; }
            [Parameter("tuple[]", "lineItem", 2)]
            public virtual List<LineItem> LineItem { get; set; }
            [Parameter("uint256", "customerId", 3)]
            public virtual BigInteger CustomerId { get; set; }
        }

        public partial class LineItem : LineItemBase { }

        public class LineItemBase
        {
            [Parameter("uint256", "id", 1)]
            public virtual BigInteger Id { get; set; }
            [Parameter("uint256", "productId", 2)]
            public virtual BigInteger ProductId { get; set; }
            [Parameter("uint256", "quantity", 3)]
            public virtual BigInteger Quantity { get; set; }
            [Parameter("string", "description", 4)]
            public virtual string Description { get; set; }
        }
        public partial class StructsSample2Deployment : StructsSample2DeploymentBase
        {
            public StructsSample2Deployment() : base(BYTECODE) { }
            public StructsSample2Deployment(string byteCode) : base(byteCode) { }
        }

        public class StructsSample2DeploymentBase : ContractDeploymentMessage
        {
            public static string BYTECODE = "60806040523480156200001157600080fd5b50600180556103e8600355620000266200035e565b506040805160808101825260018082526064602080840191825260028486018181528651808801909752600687527f68656c6c6f310000000000000000000000000000000000000000000000000000878401526060860196875281549485018083556000929092528551600490950260008051602062001774833981519152810195865593516000805160206200179483398151915285015551600080516020620017b483398151915284015594518051949594869493620000fe93600080516020620017d483398151915290910192019062000386565b505050506200010c6200035e565b5060408051608081018252600280825260c8602080840191825260038486019081528551808701909652600686527f68656c6c6f3200000000000000000000000000000000000000000000000000008683015260608501958652835460018101808655600095909552855160049091026000805160206200177483398151915281019182559351600080516020620017948339815191528501559051600080516020620017b48339815191528401559451805194959394869493620001e793600080516020620017d483398151915290910192019062000386565b50505050620001f56200035e565b50604080516080810182526003815261012c602080830191825260048385018181528551808701909652600686527f68656c6c6f33000000000000000000000000000000000000000000000000000086840152606085019586526002805460018101808355600092909252865193026000805160206200177483398151915281019384559451600080516020620017948339815191528601559051600080516020620017b48339815191528501559451805194959486949293620002cd93600080516020620017d48339815191520192019062000386565b5050600480546001818101808455600093909352805460039092027f8a35acfbc15ff81a39ae7d344fd709f28e8600b4aa8c65c6b64bfe7fe36bd19b810192835560028054949650919450919262000349927f8a35acfbc15ff81a39ae7d344fd709f28e8600b4aa8c65c6b64bfe7fe36bd19c0191906200040b565b5060029182015491015550620005cb92505050565b6040518060800160405280600081526020016000815260200160008152602001606081525090565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f10620003c957805160ff1916838001178555620003f9565b82800160010185558215620003f9579182015b82811115620003f9578251825591602001919060010190620003dc565b5062000407929150620004aa565b5090565b8280548282559060005260206000209060040281019282156200049c5760005260206000209160040282015b828111156200049c5782826000820154816000015560018201548160010155600282015481600201556003820181600301908054600181600116156101000203166002900462000489929190620004ca565b5050509160040191906004019062000437565b506200040792915062000544565b620004c791905b80821115620004075760008155600101620004b1565b90565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f10620005055780548555620003f9565b82800160010185558215620003f957600052602060002091601f016020900482015b82811115620003f957825482559160010191906001019062000527565b620004c791905b8082111562000407576000808255600182018190556002820181905562000576600383018262000580565b506004016200054b565b50805460018160011615610100020316600290046000825580601f10620005a85750620005c8565b601f016020900490600052602060002090810190620005c89190620004aa565b50565b61119980620005db6000396000f3fe608060405234801561001057600080fd5b50600436106100625760003560e01c80631a9542af14610067578063793ce7601461007c578063815c844d1461009a578063a08f28cc146100ad578063cc0b4b02146100c2578063f79eb4a2146100d5575b600080fd5b61007a610075366004610ad3565b6100e8565b005b610084610153565b604051610091919061102e565b60405180910390f35b6100846100a8366004610b45565b6102bb565b6100b561040e565b604051610091919061101d565b61007a6100d0366004610b63565b6105a0565b61007a6100e3366004610b10565b6106be565b60005b81518110156101185761011082828151811061010357fe5b60200260200101516106be565b6001016100eb565b507f63d0df058c364c605130a4550879b03d3814f0ba56c550569be936f3c2d7a2f581604051610148919061101d565b60405180910390a150565b61015b610798565b600460008154811061016957fe5b90600052602060002090600302016040518060600160405290816000820154815260200160018201805480602002602001604051908101604052809291908181526020016000905b828210156102a35783829060005260206000209060040201604051806080016040529081600082015481526020016001820154815260200160028201548152602001600382018054600181600116156101000203166002900480601f01602080910402602001604051908101604052809291908181526020018280546001816001161561010002031660029004801561028b5780601f106102605761010080835404028352916020019161028b565b820191906000526020600020905b81548152906001019060200180831161026e57829003601f168201915b505050505081525050815260200190600101906101b1565b50505050815260200160028201548152505090505b90565b6102c3610798565b6000828152602081815260408083208151606081018352815481526001820180548451818702810187019095528085529195929486810194939192919084015b828210156103f55783829060005260206000209060040201604051806080016040529081600082015481526020016001820154815260200160028201548152602001600382018054600181600116156101000203166002900480601f0160208091040260200160405190810160405280929190818152602001828054600181600116156101000203166002900480156103dd5780601f106103b2576101008083540402835291602001916103dd565b820191906000526020600020905b8154815290600101906020018083116103c057829003601f168201915b50505050508152505081526020019060010190610303565b5050505081526020016002820154815250509050919050565b60606004805480602002602001604051908101604052809291908181526020016000905b8282101561059757838290600052602060002090600302016040518060600160405290816000820154815260200160018201805480602002602001604051908101604052809291908181526020016000905b828210156105765783829060005260206000209060040201604051806080016040529081600082015481526020016001820154815260200160028201548152602001600382018054600181600116156101000203166002900480601f01602080910402602001604051908101604052809291908181526020018280546001816001161561010002031660029004801561055e5780601f106105335761010080835404028352916020019161055e565b820191906000526020600020905b81548152906001019060200180831161054157829003601f168201915b50505050508152505081526020019060010190610484565b50505050815260200160028201548152505081526020019060010190610432565b50505050905090565b60005b8151811015610634576000808481526020019081526020016000206001018282815181106105cd57fe5b602090810291909101810151825460018181018086556000958652948490208351600490930201918255828401519082015560408201516002820155606082015180519293919261062492600385019201906107b9565b5050600190920191506105a39050565b507f13fdaebbac9da33d495b4bd32c83e33786a010730713d20c5a8ef70ca576be6533838360405161066893929190610ff0565b60405180910390a17f88ab28750130223a530a1325799e7ef636cd4c7a60d350c38c45316082fdbbf8336000808581526020019081526020016000206040516106b2929190610fd0565b60405180910390a15050565b805160009081526020819052604080822083518155908301516002820155905b8260200151518110156107665781600101836020015182815181106106ff57fe5b602090810291909101810151825460018181018086556000958652948490208351600490930201918255828401519082015560408201516002820155606082015180519293919261075692600385019201906107b9565b5050600190920191506106de9050565b507f88ab28750130223a530a1325799e7ef636cd4c7a60d350c38c45316082fdbbf833836040516106b2929190610fb0565b60405180606001604052806000815260200160608152602001600081525090565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f106107fa57805160ff1916838001178555610827565b82800160010185558215610827579182015b8281111561082757825182559160200191906001019061080c565b50610833929150610837565b5090565b6102b891905b80821115610833576000815560010161083d565b600082601f83011261086257600080fd5b813561087561087082611066565b61103f565b81815260209384019390925082018360005b838110156108b3578135860161089d88826109c6565b8452506020928301929190910190600101610887565b5050505092915050565b600082601f8301126108ce57600080fd5b81356108dc61087082611066565b81815260209384019390925082018360005b838110156108b3578135860161090488826109c6565b84525060209283019291909101906001016108ee565b600082601f83011261092b57600080fd5b813561093961087082611066565b81815260209384019390925082018360005b838110156108b357813586016109618882610a4d565b845250602092830192919091019060010161094b565b600082601f83011261098857600080fd5b813561099661087082611087565b915080825260208301602083018583830111156109b257600080fd5b6109bd838284611106565b50505092915050565b6000608082840312156109d857600080fd5b6109e2608061103f565b905060006109f08484610ac0565b8252506020610a0184848301610ac0565b6020830152506040610a1584828501610ac0565b604083015250606082013567ffffffffffffffff811115610a3557600080fd5b610a4184828501610977565b60608301525092915050565b600060608284031215610a5f57600080fd5b610a69606061103f565b90506000610a778484610ac0565b825250602082013567ffffffffffffffff811115610a9457600080fd5b610aa084828501610851565b6020830152506040610ab484828501610ac0565b60408301525092915050565b6000610acc82356102b8565b9392505050565b600060208284031215610ae557600080fd5b813567ffffffffffffffff811115610afc57600080fd5b610b088482850161091a565b949350505050565b600060208284031215610b2257600080fd5b813567ffffffffffffffff811115610b3957600080fd5b610b0884828501610a4d565b600060208284031215610b5757600080fd5b6000610b088484610ac0565b60008060408385031215610b7657600080fd5b6000610b828585610ac0565b925050602083013567ffffffffffffffff811115610b9f57600080fd5b610bab858286016108bd565b9150509250929050565b6000610acc8383610e2d565b6000610acc8383610e88565b6000610acc8383610f00565b610be2816110e4565b82525050565b6000610bf3826110c1565b610bfd81856110cf565b935083602082028501610c0f856110af565b60005b84811015610c46578383038852610c2a838351610bb5565b9250610c35826110af565b602098909801979150600101610c12565b50909695505050505050565b6000610c5d826110c1565b610c6781856110cf565b935083602082028501610c79856110af565b60005b84811015610c46578383038852610c94838351610bb5565b9250610c9f826110af565b602098909801979150600101610c7c565b6000610cbb826110c5565b610cc581856110cf565b935083602082028501610cd7856110b5565b60005b84811015610c46578383038852610cf18383610bc1565b9250610cfc826110c9565b602098909801979150600101610cda565b6000610d18826110c1565b610d2281856110cf565b935083602082028501610d34856110af565b60005b84811015610c46578383038852610d4f838351610bcd565b9250610d5a826110af565b602098909801979150600101610d37565b6000610d76826110c1565b610d8081856110cf565b9350610d90818560208601611112565b610d9981611155565b9093019392505050565b600081546001811660008114610dc05760018114610de657610e25565b607f6002830416610dd181876110cf565b60ff1984168152955050602085019250610e25565b60028204610df481876110cf565b9550610dff856110b5565b60005b82811015610e1e57815488820152600190910190602001610e02565b8701945050505b505092915050565b80516000906080840190610e418582610fa7565b506020830151610e546020860182610fa7565b506040830151610e676040860182610fa7565b5060608301518482036060860152610e7f8282610d6b565b95945050505050565b80546000906080840190610e9b81611142565b610ea58682610fa7565b50506001830154610eb581611142565b610ec26020870182610fa7565b50506002830154610ed281611142565b610edf6040870182610fa7565b50600384018583036060870152610ef68382610da3565b9695505050505050565b80516000906060840190610f148582610fa7565b5060208301518482036020860152610f2c8282610c52565b9150506040830151610f416040860182610fa7565b509392505050565b80546000906060840190610f5c81611142565b610f668682610fa7565b50600184018583036020870152610f7d8382610cb0565b92505060028401549050610f9081611142565b610f9d6040870182610fa7565b5090949350505050565b610be2816102b8565b60408101610fbe8285610bd9565b8181036020830152610b088184610f00565b60408101610fde8285610bd9565b8181036020830152610b088184610f49565b60608101610ffe8286610bd9565b61100b6020830185610fa7565b8181036040830152610e7f8184610be8565b60208082528101610acc8184610d0d565b60208082528101610acc8184610f00565b60405181810167ffffffffffffffff8111828210171561105e57600080fd5b604052919050565b600067ffffffffffffffff82111561107d57600080fd5b5060209081020190565b600067ffffffffffffffff82111561109e57600080fd5b506020601f91909101601f19160190565b60200190565b60009081526020902090565b5190565b5490565b60010190565b90815260200190565b6001600160a01b031690565b60006110ef826110f5565b92915050565b60006110ef8260006110ef826110d8565b82818337506000910152565b60005b8381101561112d578181015183820152602001611115565b8381111561113c576000848401525b50505050565b60006110ef611150836102b8565b6102b8565b601f01601f19169056fea265627a7a72305820d86be1a4bba1bef3e684f798e114d9c22018925c426cdb9ca60314aec45501f46c6578706572696d656e74616cf50037405787fa12a823e0f2b7631cc41b3ba8828b3321ca811111fa75cd3aa3bb5ace405787fa12a823e0f2b7631cc41b3ba8828b3321ca811111fa75cd3aa3bb5acf405787fa12a823e0f2b7631cc41b3ba8828b3321ca811111fa75cd3aa3bb5ad0405787fa12a823e0f2b7631cc41b3ba8828b3321ca811111fa75cd3aa3bb5ad1";
            public StructsSample2DeploymentBase() : base(BYTECODE) { }
            public StructsSample2DeploymentBase(string byteCode) : base(byteCode) { }

        }

        public partial class SetPurchaseOrdersFunction : SetPurchaseOrdersFunctionBase { }

        [Function("SetPurchaseOrders")]
        public class SetPurchaseOrdersFunctionBase : FunctionMessage
        {
            [Parameter("tuple[]", "purchaseOrder", 1)]
            public virtual List<PurchaseOrder> PurchaseOrder { get; set; }
        }

        public partial class GetPurchaseOrder2Function : GetPurchaseOrder2FunctionBase { }

        [Function("GetPurchaseOrder2", "tuple")]
        public class GetPurchaseOrder2FunctionBase : FunctionMessage
        {

        }

        public partial class GetPurchaseOrderFunction : GetPurchaseOrderFunctionBase { }

        [Function("GetPurchaseOrder", "tuple")]
        public class GetPurchaseOrderFunctionBase : FunctionMessage
        {
            [Parameter("uint256", "id", 1)]
            public virtual BigInteger Id { get; set; }
        }

        public partial class GetPurchaseOrder3Function : GetPurchaseOrder3FunctionBase { }

        [Function("GetPurchaseOrder3", "tuple[]")]
        public class GetPurchaseOrder3FunctionBase : FunctionMessage
        {

        }

        public partial class AddLineItemsFunction : AddLineItemsFunctionBase { }

        [Function("AddLineItems")]
        public class AddLineItemsFunctionBase : FunctionMessage
        {
            [Parameter("uint256", "id", 1)]
            public virtual BigInteger Id { get; set; }
            [Parameter("tuple[]", "lineItem", 2)]
            public virtual List<LineItem> LineItem { get; set; }
        }

        public partial class SetPurchaseOrderFunction : SetPurchaseOrderFunctionBase { }

        [Function("SetPurchaseOrder")]
        public class SetPurchaseOrderFunctionBase : FunctionMessage
        {
            [Parameter("tuple", "purchaseOrder", 1)]
            public virtual PurchaseOrder PurchaseOrder { get; set; }
        }

        public partial class PurchaseOrderChangedEventDTO : PurchaseOrderChangedEventDTOBase { }

        [Event("PurchaseOrderChanged")]
        public class PurchaseOrderChangedEventDTOBase : IEventDTO
        {
            [Parameter("address", "sender", 1, false)]
            public virtual string Sender { get; set; }
            [Parameter("tuple", "purchaseOrder", 2, false)]
            public virtual PurchaseOrder PurchaseOrder { get; set; }
        }

        public partial class PurchaseOrdersChangedEventDTO : PurchaseOrdersChangedEventDTOBase { }

        [Event("PurchaseOrdersChanged")]
        public class PurchaseOrdersChangedEventDTOBase : IEventDTO
        {
            [Parameter("tuple[]", "purchaseOrder", 1, false)]
            public virtual List<PurchaseOrder> PurchaseOrder { get; set; }
        }

        public partial class LineItemsAddedEventDTO : LineItemsAddedEventDTOBase { }

        [Event("LineItemsAdded")]
        public class LineItemsAddedEventDTOBase : IEventDTO
        {
            [Parameter("address", "sender", 1, false)]
            public virtual string Sender { get; set; }
            [Parameter("uint256", "purchaseOrderId", 2, false)]
            public virtual BigInteger PurchaseOrderId { get; set; }
            [Parameter("tuple[]", "lineItem", 3, false)]
            public virtual List<LineItem> LineItem { get; set; }
        }



        public partial class GetPurchaseOrder2OutputDTO : GetPurchaseOrder2OutputDTOBase { }

        [FunctionOutput]
        public class GetPurchaseOrder2OutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("tuple", "purchaseOrder", 1)]
            public virtual PurchaseOrder PurchaseOrder { get; set; }
        }

        public partial class GetPurchaseOrderOutputDTO : GetPurchaseOrderOutputDTOBase { }

        [FunctionOutput]
        public class GetPurchaseOrderOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("tuple", "purchaseOrder", 1)]
            public virtual PurchaseOrder PurchaseOrder { get; set; }
        }

        public partial class GetPurchaseOrder3OutputDTO : GetPurchaseOrder3OutputDTOBase { }

        [FunctionOutput]
        public class GetPurchaseOrder3OutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("tuple[]", "purchaseOrder", 1)]
            public virtual List<PurchaseOrder> PurchaseOrder { get; set; }
        }

    }
}