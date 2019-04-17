using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Web3.Accounts;
using Nethereum.XUnitEthereumClients;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class TestInternalArrayOfStructs
    {
        /*
pragma solidity "0.5.7";
pragma experimental ABIEncoderV2;

contract StructsSample
{
        mapping(uint => PurchaseOrder) purchaseOrders;
        event PurchaseOrderChanged(address sender, PurchaseOrder purchaseOrder);
        event LineItemsAdded(address sender, uint purchaseOrderId, LineItem[] lineItem);
        
        struct PurchaseOrder {
            uint256 id;
            LineItem[] lineItem;
            uint256 customerId;
        }

        struct LineItem {
            uint256 id;
            uint256 productId;
            uint256 quantity;
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
        }

        function GetPurchaseOrder(uint id) view public returns (PurchaseOrder memory purchaseOrder) {
           return purchaseOrders[id];
        }

        function GetPurchaseOrder2() public returns (PurchaseOrder memory purchaseOrder) {
           // return storedPurchaseOrder;
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

        [Fact]
        public void ShouldEncodeSignatureWithStructArrays()
        {
            var functionAbi = ABITypedRegistry.GetFunctionABI<SetPurchaseOrderFunction>();
            Assert.Equal("0cc400bd", functionAbi.Sha3Signature);

            functionAbi = ABITypedRegistry.GetFunctionABI<SetPurchaseOrdersFunction>();
            Assert.Equal("cfca7768", functionAbi.Sha3Signature);   
        }


        [Fact]
        public async void ShouldEncodeDecodeStructContainingStructsArray()
        {
            var address = "0x12890d2cce102216644c59daE5baed380d84830c";
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var web3 = new Web3.Web3(new Account(privateKey));
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<StructsSampleDeployment>()
                .SendRequestAndWaitForReceiptAsync();

            var purchaseOrder = new PurchaseOrder();
            purchaseOrder.CustomerId = 1000;
            purchaseOrder.Id = 1;
            purchaseOrder.LineItem = new List<LineItem>();
            purchaseOrder.LineItem.Add(new LineItem() { Id = 1, ProductId = 100, Quantity = 2 });
            purchaseOrder.LineItem.Add(new LineItem() { Id = 2, ProductId = 200, Quantity = 3 });

            var contractHandler = web3.Eth.GetContractHandler(deploymentReceipt.ContractAddress);

            var receiptSending = await contractHandler.SendRequestAndWaitForReceiptAsync(new SetPurchaseOrderFunction() { PurchaseOrder = purchaseOrder });
            var eventPurchaseOrder = contractHandler.GetEvent<PurchaseOrderChangedEventDTO>();
            var eventOutputs = eventPurchaseOrder.DecodeAllEventsForEvent(receiptSending.Logs);
            var purchaseOrderResult = eventOutputs[0].Event.PurchaseOrder;
            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);
            Assert.Equal(2, purchaseOrderResult.LineItem[1].Id);
            Assert.Equal(200, purchaseOrderResult.LineItem[1].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[1].Quantity);

            var query = await contractHandler.QueryDeserializingToObjectAsync<GetPurchaseOrderFunction, GetPurchaseOrderOutputDTO>(new GetPurchaseOrderFunction() { Id = 1 });

             purchaseOrderResult = query.PurchaseOrder;
            Assert.Equal(1, purchaseOrderResult.Id);
            Assert.Equal(1000, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(100, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].Quantity);
            Assert.Equal(2, purchaseOrderResult.LineItem[1].Id);
            Assert.Equal(200, purchaseOrderResult.LineItem[1].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[1].Quantity);


            var lineItems = new List<LineItem>();
            lineItems.Add(new LineItem() { Id = 3, ProductId = 300, Quantity = 2 });
            lineItems.Add(new LineItem() { Id = 4, ProductId = 400, Quantity = 3 });

            var lineItemsFunction = new AddLineItemsFunction() { Id = 1, LineItem = lineItems };
            var data = lineItemsFunction.GetCallData().ToHex();

            receiptSending = await contractHandler.SendRequestAndWaitForReceiptAsync(new AddLineItemsFunction() { Id =1, LineItem = lineItems });

            var lineItemsEvent = contractHandler.GetEvent<LineItemsAddedEventDTO>();
            var lineItemsLogs = lineItemsEvent.DecodeAllEventsForEvent(receiptSending.Logs);
            query = await contractHandler.QueryDeserializingToObjectAsync<GetPurchaseOrderFunction, GetPurchaseOrderOutputDTO>(new GetPurchaseOrderFunction() { Id = 1 });
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
        }


        public partial class StructsSampleDeployment : StructsSampleDeploymentBase
        {
            public StructsSampleDeployment() : base(BYTECODE) { }
            public StructsSampleDeployment(string byteCode) : base(byteCode) { }
        }

        public class StructsSampleDeploymentBase : ContractDeploymentMessage
        {
            public static string BYTECODE = "608060405234801561001057600080fd5b50610a4c806100206000396000f3fe608060405234801561001057600080fd5b50600436106100575760003560e01c80630cc400bd1461005c578063793ce76014610071578063815c844d1461008f578063cd21ca74146100a2578063cfca7768146100b5575b600080fd5b61006f61006a3660046105d3565b6100c8565b005b610079610188565b604051610086919061094f565b60405180910390f35b61007961009d366004610608565b610193565b61006f6100b0366004610626565b610244565b61006f6100c3366004610596565b610330565b805160009081526020819052604080822083518155908301516002820155905b82602001515181101561014a57816001018360200151828151811061010957fe5b6020908102919091018101518254600181810185556000948552938390208251600390920201908155918101518284015560400151600290910155016100e8565b507f0dc5f52079349ee37fd5e887dcc0f4f87fd42b39457373a22bd627d904b512d3338360405161017c9291906108d9565b60405180910390a15050565b610190610364565b90565b61019b610364565b6000828152602081815260408083208151606081018352815481526001820180548451818702810187019095528085529195929486810194939192919084015b8282101561022b57838290600052602060002090600302016040518060600160405290816000820154815260200160018201548152602001600282015481525050815260200190600101906101db565b5050505081526020016002820154815250509050919050565b60005b81518110156102b25760008084815260200190815260200160002060010182828151811061027157fe5b602090810291909101810151825460018181018555600094855293839020825160039092020190815591810151828401556040015160029091015501610247565b507f82aa45b3f2e54dab763d30d887917f42ea610ef707f2d22c9f8c13dda3edff8f3383836040516102e693929190610919565b60405180910390a17f0dc5f52079349ee37fd5e887dcc0f4f87fd42b39457373a22bd627d904b512d33360008085815260200190815260200160002060405161017c9291906108f9565b60005b81518110156103605761035882828151811061034b57fe5b60200260200101516100c8565b600101610333565b5050565b60405180606001604052806000815260200160608152602001600081525090565b600082601f83011261039657600080fd5b81356103a96103a482610987565b610960565b915081818352602084019350602081019050838560608402820111156103ce57600080fd5b60005b838110156103fc57816103e488826104d5565b845250602090920191606091909101906001016103d1565b5050505092915050565b600082601f83011261041757600080fd5b81356104256103a482610987565b9150818183526020840193506020810190508385606084028201111561044a57600080fd5b60005b838110156103fc578161046088826104d5565b8452506020909201916060919091019060010161044d565b600082601f83011261048957600080fd5b81356104976103a482610987565b81815260209384019390925082018360005b838110156103fc57813586016104bf8882610530565b84525060209283019291909101906001016104a9565b6000606082840312156104e757600080fd5b6104f16060610960565b905060006104ff8484610583565b825250602061051084848301610583565b602083015250604061052484828501610583565b60408301525092915050565b60006060828403121561054257600080fd5b61054c6060610960565b9050600061055a8484610583565b825250602082013567ffffffffffffffff81111561057757600080fd5b61051084828501610385565b600061058f8235610190565b9392505050565b6000602082840312156105a857600080fd5b813567ffffffffffffffff8111156105bf57600080fd5b6105cb84828501610478565b949350505050565b6000602082840312156105e557600080fd5b813567ffffffffffffffff8111156105fc57600080fd5b6105cb84828501610530565b60006020828403121561061a57600080fd5b60006105cb8484610583565b6000806040838503121561063957600080fd5b60006106458585610583565b925050602083013567ffffffffffffffff81111561066257600080fd5b61066e85828601610406565b9150509250929050565b60006106848383610791565b505060600190565b600061068483836107ce565b6106a1816109dd565b82525050565b60006106b2826109ba565b6106bc81856109c8565b93506106c7836109a8565b60005b828110156106f2576106dd868351610678565b95506106e8826109a8565b91506001016106ca565b5093949350505050565b6000610707826109ba565b61071181856109c8565b935061071c836109a8565b60005b828110156106f257610732868351610678565b955061073d826109a8565b915060010161071f565b6000610752826109be565b61075c81856109c8565b9350610767836109ae565b60005b828110156106f25761077c868361068c565b9550610787826109c2565b915060010161076a565b805160608301906107a284826108d0565b5060208201516107b560208501826108d0565b5060408201516107c860408501826108d0565b50505050565b805460608301906107de816109ff565b6107e885826108d0565b505060018201546107f8816109ff565b61080560208601826108d0565b50506002820154610815816109ff565b61082260408601826108d0565b5050505050565b8051600090606084019061083d85826108d0565b506020830151848203602086015261085582826106fc565b915050604083015161086a60408601826108d0565b509392505050565b80546000906060840190610885816109ff565b61088f86826108d0565b506001840185830360208701526108a68382610747565b925050600284015490506108b9816109ff565b6108c660408701826108d0565b5090949350505050565b6106a181610190565b604081016108e78285610698565b81810360208301526105cb8184610829565b604081016109078285610698565b81810360208301526105cb8184610872565b606081016109278286610698565b61093460208301856108d0565b818103604083015261094681846106a7565b95945050505050565b6020808252810161058f8184610829565b60405181810167ffffffffffffffff8111828210171561097f57600080fd5b604052919050565b600067ffffffffffffffff82111561099e57600080fd5b5060209081020190565b60200190565b60009081526020902090565b5190565b5490565b60010190565b90815260200190565b6001600160a01b031690565b60006109e8826109ee565b92915050565b60006109e88260006109e8826109d1565b60006109e8610a0d83610190565b61019056fea265627a7a723058200f803aec6e4a7174857b46099a96be3697a71b30f55bd4afc053bc687ef7a57c6c6578706572696d656e74616cf50037";
            public StructsSampleDeploymentBase() : base(BYTECODE) { }
            public StructsSampleDeploymentBase(string byteCode) : base(byteCode) { }

        }

        public partial class SetPurchaseOrderFunction : SetPurchaseOrderFunctionBase { }

        [Function("SetPurchaseOrder")]
        public class SetPurchaseOrderFunctionBase : FunctionMessage
        {
            [Parameter("tuple", "purchaseOrder", 1)]
            public virtual PurchaseOrder PurchaseOrder { get; set; }
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

        public partial class AddLineItemsFunction : AddLineItemsFunctionBase { }

        [Function("AddLineItems")]
        public class AddLineItemsFunctionBase : FunctionMessage
        {
            [Parameter("uint256", "id", 1)]
            public virtual BigInteger Id { get; set; }
            [Parameter("tuple[]", "lineItem", 2)]
            public virtual List<LineItem> LineItem { get; set; }
        }

        public partial class SetPurchaseOrdersFunction : SetPurchaseOrdersFunctionBase { }

        [Function("SetPurchaseOrders")]
        public class SetPurchaseOrdersFunctionBase : FunctionMessage
        {
            [Parameter("tuple[]", "purchaseOrder", 1)]
            public virtual List<PurchaseOrder> PurchaseOrder { get; set; }
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


        public partial class GetPurchaseOrderOutputDTO : GetPurchaseOrderOutputDTOBase { }

        [FunctionOutput]
        public class GetPurchaseOrderOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("tuple", "purchaseOrder", 1)]
            public virtual PurchaseOrder PurchaseOrder { get; set; }
        }
    }

}