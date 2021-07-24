using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Web3.Accounts;
using Nethereum.XUnitEthereumClients;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Nethereum.ABI.FunctionEncoding;

namespace Nethereum.Accounts.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class TestInternalDynamicArrayOfNonDynamicStructs
    {

        /*
pragma solidity "0.5.7";
pragma experimental ABIEncoderV2;

contract StructsSample
{
        mapping(uint => PurchaseOrder) purchaseOrders;
        event PurchaseOrderChanged(address sender, PurchaseOrder purchaseOrder);
        event PurchaseOrdersChanged(PurchaseOrder[] purchaseOrder);
        event LineItemsAdded(address sender, uint purchaseOrderId, LineItem[] lineItem);

        //array
        PurchaseOrder[] purchaseOrdersArray;
        PurchaseOrder _purchaseOrder;

         constructor() public {
            _purchaseOrder.id = 1;
            _purchaseOrder.customerId = 2;
            LineItem memory lineItem = LineItem(1,2,3);
            _purchaseOrder.lineItem.push(lineItem);
            purchaseOrdersArray.push(_purchaseOrder); 
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

        function GetPurchaseOrder2() public returns (PurchaseOrder memory purchaseOrder) {
           // return storedPurchaseOrder;
        }

         function GetPurchaseOrders() public view returns (PurchaseOrder[] memory purchaseOrder) {
            return purchaseOrdersArray;
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

        public TestInternalDynamicArrayOfNonDynamicStructs(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

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
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<StructsSampleDeployment>()
                .SendRequestAndWaitForReceiptAsync().ConfigureAwait(false);

            var purchaseOrder = new PurchaseOrder();
            purchaseOrder.CustomerId = 1000;
            purchaseOrder.Id = 1;
            purchaseOrder.LineItem = new List<LineItem>();
            purchaseOrder.LineItem.Add(new LineItem() { Id = 1, ProductId = 100, Quantity = 2 });
            purchaseOrder.LineItem.Add(new LineItem() { Id = 2, ProductId = 200, Quantity = 3 });

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
            Assert.Equal(2, purchaseOrderResult.LineItem[1].Id);
            Assert.Equal(200, purchaseOrderResult.LineItem[1].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[1].Quantity);

          

            var query = await contractHandler.QueryDeserializingToObjectAsync<GetPurchaseOrderFunction, GetPurchaseOrderOutputDTO>(new GetPurchaseOrderFunction() { Id = 1 }).ConfigureAwait(false);

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

            receiptSending = await contractHandler.SendRequestAndWaitForReceiptAsync(new AddLineItemsFunction() { Id =1, LineItem = lineItems }).ConfigureAwait(false);

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
            var query2 = await contractHandler.QueryDeserializingToObjectAsync<GetPurchaseOrdersFunction, GetPurchaseOrdersOutputDTO>().ConfigureAwait(false);
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
            Assert.Equal(2, purchaseOrderResult.CustomerId);
            Assert.Equal(1, purchaseOrderResult.LineItem[0].Id);
            Assert.Equal(2, purchaseOrderResult.LineItem[0].ProductId);
            Assert.Equal(3, purchaseOrderResult.LineItem[0].Quantity);

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
            public static string BYTECODE = "608060405234801561001057600080fd5b5060016002908155600455610023610148565b50604080516060810182526001808252600260208301818152600394840185815285548085018755600087815286517fc2575a0e9e593c00f959f8c92f12db2869c3395a3b0502d05e2516446f71f85b9289029283015592517fc2575a0e9e593c00f959f8c92f12db2869c3395a3b0502d05e2516446f71f85c82015590517fc2575a0e9e593c00f959f8c92f12db2869c3395a3b0502d05e2516446f71f85d9091015582548084018085559390915281547fb10e2d527612073b26eecdfd717e6a320cf44b4afac2b0732d9fcbe2b7fa0cf691860291820190815585549495939492939092610136927fb10e2d527612073b26eecdfd717e6a320cf44b4afac2b0732d9fcbe2b7fa0cf7019190610169565b50600291820154910155506101fd9050565b60405180606001604052806000815260200160008152602001600081525090565b8280548282559060005260206000209060030281019282156101c35760005260206000209160030282015b828111156101c35782548255600180840154908301556002808401549083015560039283019290910190610194565b506101cf9291506101d3565b5090565b6101fa91905b808211156101cf5760008082556001820181905560028201556003016101d9565b90565b610c1a8061020c6000396000f3fe608060405234801561001057600080fd5b50600436106100625760003560e01c80630cc400bd14610067578063793ce7601461007c578063815c844d1461009a578063bde3a813146100ad578063cd21ca74146100c2578063cfca7768146100d5575b600080fd5b61007a61007536600461071a565b6100e8565b005b6100846101a8565b6040516100919190610b1d565b60405180910390f35b6100846100a836600461074f565b6101b3565b6100b5610264565b6040516100919190610b0c565b61007a6100d036600461076d565b610354565b61007a6100e33660046106dd565b610440565b805160009081526020819052604080822083518155908301516002820155905b82602001515181101561016a57816001018360200151828151811061012957fe5b602090810291909101810151825460018181018555600094855293839020825160039092020190815591810151828401556040015160029091015501610108565b507f0dc5f52079349ee37fd5e887dcc0f4f87fd42b39457373a22bd627d904b512d3338360405161019c929190610a96565b60405180910390a15050565b6101b06104ab565b90565b6101bb6104ab565b6000828152602081815260408083208151606081018352815481526001820180548451818702810187019095528085529195929486810194939192919084015b8282101561024b57838290600052602060002090600302016040518060600160405290816000820154815260200160018201548152602001600282015481525050815260200190600101906101fb565b5050505081526020016002820154815250509050919050565b60606001805480602002602001604051908101604052809291908181526020016000905b8282101561034b57838290600052602060002090600302016040518060600160405290816000820154815260200160018201805480602002602001604051908101604052809291908181526020016000905b8282101561032a57838290600052602060002090600302016040518060600160405290816000820154815260200160018201548152602001600282015481525050815260200190600101906102da565b50505050815260200160028201548152505081526020019060010190610288565b50505050905090565b60005b81518110156103c25760008084815260200190815260200160002060010182828151811061038157fe5b602090810291909101810151825460018181018555600094855293839020825160039092020190815591810151828401556040015160029091015501610357565b507f82aa45b3f2e54dab763d30d887917f42ea610ef707f2d22c9f8c13dda3edff8f3383836040516103f693929190610ad6565b60405180910390a17f0dc5f52079349ee37fd5e887dcc0f4f87fd42b39457373a22bd627d904b512d33360008085815260200190815260200160002060405161019c929190610ab6565b60005b81518110156104705761046882828151811061045b57fe5b60200260200101516100e8565b600101610443565b507fb14fa81a9e0940109985cca0ffab2aa902d841ae78b578e9e0c28763fce8bd6e816040516104a09190610b0c565b60405180910390a150565b60405180606001604052806000815260200160608152602001600081525090565b600082601f8301126104dd57600080fd5b81356104f06104eb82610b55565b610b2e565b9150818183526020840193506020810190508385606084028201111561051557600080fd5b60005b83811015610543578161052b888261061c565b84525060209092019160609190910190600101610518565b5050505092915050565b600082601f83011261055e57600080fd5b813561056c6104eb82610b55565b9150818183526020840193506020810190508385606084028201111561059157600080fd5b60005b8381101561054357816105a7888261061c565b84525060209092019160609190910190600101610594565b600082601f8301126105d057600080fd5b81356105de6104eb82610b55565b81815260209384019390925082018360005b8381101561054357813586016106068882610677565b84525060209283019291909101906001016105f0565b60006060828403121561062e57600080fd5b6106386060610b2e565b9050600061064684846106ca565b8252506020610657848483016106ca565b602083015250604061066b848285016106ca565b60408301525092915050565b60006060828403121561068957600080fd5b6106936060610b2e565b905060006106a184846106ca565b825250602082013567ffffffffffffffff8111156106be57600080fd5b610657848285016104cc565b60006106d682356101b0565b9392505050565b6000602082840312156106ef57600080fd5b813567ffffffffffffffff81111561070657600080fd5b610712848285016105bf565b949350505050565b60006020828403121561072c57600080fd5b813567ffffffffffffffff81111561074357600080fd5b61071284828501610677565b60006020828403121561076157600080fd5b600061071284846106ca565b6000806040838503121561078057600080fd5b600061078c85856106ca565b925050602083013567ffffffffffffffff8111156107a957600080fd5b6107b58582860161054d565b9150509250929050565b60006107cb838361094e565b505060600190565b60006107cb838361098b565b60006106d683836109e6565b6107f481610bab565b82525050565b600061080582610b88565b61080f8185610b96565b935061081a83610b76565b60005b82811015610845576108308683516107bf565b955061083b82610b76565b915060010161081d565b5093949350505050565b600061085a82610b88565b6108648185610b96565b935061086f83610b76565b60005b82811015610845576108858683516107bf565b955061089082610b76565b9150600101610872565b60006108a582610b8c565b6108af8185610b96565b93506108ba83610b7c565b60005b82811015610845576108cf86836107d3565b95506108da82610b90565b91506001016108bd565b60006108ef82610b88565b6108f98185610b96565b93508360208202850161090b85610b76565b60005b848110156109425783830388526109268383516107df565b925061093182610b76565b60209890980197915060010161090e565b50909695505050505050565b8051606083019061095f8482610a8d565b5060208201516109726020850182610a8d565b5060408201516109856040850182610a8d565b50505050565b8054606083019061099b81610bcd565b6109a58582610a8d565b505060018201546109b581610bcd565b6109c26020860182610a8d565b505060028201546109d281610bcd565b6109df6040860182610a8d565b5050505050565b805160009060608401906109fa8582610a8d565b5060208301518482036020860152610a12828261084f565b9150506040830151610a276040860182610a8d565b509392505050565b80546000906060840190610a4281610bcd565b610a4c8682610a8d565b50600184018583036020870152610a63838261089a565b92505060028401549050610a7681610bcd565b610a836040870182610a8d565b5090949350505050565b6107f4816101b0565b60408101610aa482856107eb565b818103602083015261071281846109e6565b60408101610ac482856107eb565b81810360208301526107128184610a2f565b60608101610ae482866107eb565b610af16020830185610a8d565b8181036040830152610b0381846107fa565b95945050505050565b602080825281016106d681846108e4565b602080825281016106d681846109e6565b60405181810167ffffffffffffffff81118282101715610b4d57600080fd5b604052919050565b600067ffffffffffffffff821115610b6c57600080fd5b5060209081020190565b60200190565b60009081526020902090565b5190565b5490565b60010190565b90815260200190565b6001600160a01b031690565b6000610bb682610bbc565b92915050565b6000610bb6826000610bb682610b9f565b6000610bb6610bdb836101b0565b6101b056fea265627a7a7230582096df729aef8b0d0a5f1e087647bffe76396ead26a673478e4eecafdf97687e2a6c6578706572696d656e74616cf50037";
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

        public partial class GetPurchaseOrdersFunction : GetPurchaseOrdersFunctionBase { }

        [Function("GetPurchaseOrders", "tuple[]")]
        public class GetPurchaseOrdersFunctionBase : FunctionMessage
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


        public partial class GetPurchaseOrderOutputDTO : GetPurchaseOrderOutputDTOBase { }

        [FunctionOutput]
        public class GetPurchaseOrderOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("tuple", "purchaseOrder", 1)]
            public virtual PurchaseOrder PurchaseOrder { get; set; }
        }

        public partial class GetPurchaseOrdersOutputDTO : GetPurchaseOrdersOutputDTOBase { }

        [FunctionOutput]
        public class GetPurchaseOrdersOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("tuple[]", "purchaseOrder", 1)]
            public virtual List<PurchaseOrder> PurchaseOrder { get; set; }
        }

    }
}
