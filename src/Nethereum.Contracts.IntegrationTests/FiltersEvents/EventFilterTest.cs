using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.CQS;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.FiltersEvents
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EventFilterTest
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public EventFilterTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async Task TestEvent()
        {
            var senderAddress = AccountFactory.Address;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentMessage = new TestContract3Deployment() { FromAddress = senderAddress };
            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<TestContract3Deployment>();
            var transactionReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
            var contractHandler = web3.Eth.GetContractHandler(transactionReceipt.ContractAddress);

            var eventFilter = contractHandler.GetEvent<ItemCreatedEventDTO>();
            var filterId = await eventFilter.CreateFilterAsync(1);

            var transactionReceiptSend = await contractHandler.SendRequestAndWaitForReceiptAsync(new NewItemFunction()
            {
                FromAddress = senderAddress,
                Id = 1,
                Price = 100
            });

            var result = await eventFilter.GetFilterChanges(filterId);

            Assert.Single(result);
        }

        [Event("ItemCreated")]
        public class ItemCreatedEventDTO:IEventDTO
        {
            [Parameter("uint256", "itemId", 1, true)]
            public BigInteger ItemId { get; set; }
            [Parameter("address", "result", 2, false)]
            public string Result { get; set; }
        }

        public class TestContract3Deployment : ContractDeploymentMessage
        {
            public static string BYTECODE = "6060604052341561000f57600080fd5b60018054600160a060020a03191633600160a060020a03161790556101fe806100396000396000f3006060604052600436106100405763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166329b856888114610045575b600080fd5b341561005057600080fd5b61005e600435602435610060565b005b6000805460018101610072838261015b565b91600052602060002090600302016000606060405190810160409081528682526020820186905260015473ffffffffffffffffffffffffffffffffffffffff169082015291905081518155602082015181600101556040820151600291909101805473ffffffffffffffffffffffffffffffffffffffff191673ffffffffffffffffffffffffffffffffffffffff909216919091179055508290507f1c78b9707d8ddf8078f46413765b0e73d250ffc795526eeb39c6889ea8efafd03360405173ffffffffffffffffffffffffffffffffffffffff909116815260200160405180910390a25050565b81548183558181151161018757600302816003028360005260206000209182019101610187919061018c565b505050565b6101cf91905b808211156101cb576000808255600182015560028101805473ffffffffffffffffffffffffffffffffffffffff19169055600301610192565b5090565b905600a165627a7a723058203753f72c36b1db5a70e27526c245d50858edb379405dddc78dea7dc6ff8ecee00029";

            public TestContract3Deployment() : base(BYTECODE) { }

            public TestContract3Deployment(string byteCode) : base(byteCode) { }
        }


        /*Contract 
         contract TestContract3 {

    struct Item {
        uint id;
        uint price;
        address manager;
    }

    Item[] items;
    address manager;

    function TestContract3() public {
        manager = msg.sender;
    }
    event ItemCreated(uint indexed itemId, address result);

    function newItem(uint id, uint price) public {
        items.push(Item(id, price, manager));
        emit ItemCreated(id, msg.sender);
    }
}
*/

        [Function("newItem")]
        public class NewItemFunction : FunctionMessage
        {
            [Parameter("uint256", "id", 1)]
            public BigInteger Id { get; set; }
            [Parameter("uint256", "price", 2)]
            public BigInteger Price { get; set; }
        }
    }
}
 