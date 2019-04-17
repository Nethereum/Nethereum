using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Web3.Accounts;
using Nethereum.XUnitEthereumClients;
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
        PurchaseOrder private storedPurchaseOrder;
        event PurchaseOrderChanged(address sender, PurchaseOrder purchaseOrder);

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
        
        address private _to;

        function Test(address payable to) public{
            _to = to;
        }

        function SetPurchaseOrder(PurchaseOrder memory purchaseOrder) public {
           // storedPurchaseOrder = purchaseOrder;
            emit PurchaseOrderChanged(msg.sender, purchaseOrder);
        }

        function GetPurchaseOrder() view public returns (PurchaseOrder memory purchaseOrder) {
            return storedPurchaseOrder;
        }

        function GetPurchaseOrder2() public returns (PurchaseOrder memory purchaseOrder) {
            return storedPurchaseOrder;
        }
}
*/

        [Fact]
        public void ShouldEncodeSignatureWithStructArrays()
        {
            var functionAbi = ABITypedRegistry.GetFunctionABI<SetPurchaseOrderFunction>();
            Assert.Equal("0cc400bd", functionAbi.Sha3Signature);
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
            public static string BYTECODE = "608060405234801561001057600080fd5b506104f3806100206000396000f3fe608060405234801561001057600080fd5b50600436106100365760003560e01c80630cc400bd1461003b578063cd8d1dbf14610050575b600080fd5b61004e6100493660046102b8565b61006e565b005b6100586100aa565b604051610065919061041c565b60405180910390f35b7f0dc5f52079349ee37fd5e887dcc0f4f87fd42b39457373a22bd627d904b512d3338260405161009f9291906103fc565b60405180910390a150565b6100b2610155565b60006040518060600160405290816000820154815260200160018201805480602002602001604051908101604052809291908181526020016000905b8282101561013e57838290600052602060002090600302016040518060600160405290816000820154815260200160018201548152602001600282015481525050815260200190600101906100ee565b505050508152602001600282015481525050905090565b60405180606001604052806000815260200160608152602001600081525090565b600082601f83011261018757600080fd5b813561019a61019582610454565b61042d565b915081818352602084019350602081019050838560608402820111156101bf57600080fd5b60005b838110156101ed57816101d588826101f7565b845250602090920191606091909101906001016101c2565b5050505092915050565b60006060828403121561020957600080fd5b610213606061042d565b9050600061022184846102a5565b8252506020610232848483016102a5565b6020830152506040610246848285016102a5565b60408301525092915050565b60006060828403121561026457600080fd5b61026e606061042d565b9050600061027c84846102a5565b825250602082013567ffffffffffffffff81111561029957600080fd5b61023284828501610176565b60006102b18235610494565b9392505050565b6000602082840312156102ca57600080fd5b813567ffffffffffffffff8111156102e157600080fd5b6102ed84828501610252565b949350505050565b6000610301838361036d565b505060600190565b61031281610497565b82525050565b60006103238261047b565b61032d818561047f565b935061033883610475565b60005b828110156103635761034e8683516102f5565b955061035982610475565b915060010161033b565b5093949350505050565b8051606083019061037e84826103f3565b50602082015161039160208501826103f3565b5060408201516103a460408501826103f3565b50505050565b805160009060608401906103be85826103f3565b50602083015184820360208601526103d68282610318565b91505060408301516103eb60408601826103f3565b509392505050565b61031281610494565b6040810161040a8285610309565b81810360208301526102ed81846103aa565b602080825281016102b181846103aa565b60405181810167ffffffffffffffff8111828210171561044c57600080fd5b604052919050565b600067ffffffffffffffff82111561046b57600080fd5b5060209081020190565b60200190565b5190565b90815260200190565b6001600160a01b031690565b90565b60006104a2826104a8565b92915050565b60006104a28260006104a28261048856fea265627a7a723058209701a924ebcd0c5581116f4c08398a2bebb056acce1f4c79d6e7bd4075aeda866c6578706572696d656e74616cf50037";
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

        public partial class GetPurchaseOrderFunction : GetPurchaseOrderFunctionBase { }

        [Function("GetPurchaseOrder", "tuple")]
        public class GetPurchaseOrderFunctionBase : FunctionMessage
        {

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



        public partial class GetPurchaseOrderOutputDTO : GetPurchaseOrderOutputDTOBase { }

        [FunctionOutput]
        public class GetPurchaseOrderOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("tuple", "purchaseOrder", 1)]
            public virtual PurchaseOrder PurchaseOrder { get; set; }
        }
    }
}