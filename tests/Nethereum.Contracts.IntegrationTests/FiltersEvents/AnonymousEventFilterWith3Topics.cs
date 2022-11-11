using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.XUnitEthereumClients;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

// ReSharper disable ConsiderUsingConfigureAwait
// ReSharper disable InconsistentNaming

namespace Nethereum.Contracts.IntegrationTests.FiltersEvents
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class AnonymousEventFilterWith3Topics
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public AnonymousEventFilterWith3Topics(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async Task TestEvent()
        {
            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentMessage = new TestAnonymousEventContractDeployment {FromAddress = senderAddress};
            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<TestAnonymousEventContractDeployment>();
            var deploymentTransactionReceipt =
                await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage).ConfigureAwait(false);
            var contractHandler = web3.Eth.GetContractHandler(deploymentTransactionReceipt.ContractAddress);

            var itemCreatedEvent = contractHandler.GetEvent<ItemCreatedEventDTO>();


            var eventFilter1__ = await itemCreatedEvent.CreateFilterAsync(1).ConfigureAwait(false);
            var eventFilter__SenderAddress =
                await itemCreatedEvent.CreateFilterAsync<object, object, string>(null, null, senderAddress).ConfigureAwait(false);

            var newItem1FunctionTransactionReceipt = await contractHandler.SendRequestAndWaitForReceiptAsync(
                new NewItemFunction
                {
                    Id = 1,
                    Price = 100
                }).ConfigureAwait(false);
            var newItem2FunctionTransactionReceipt = await contractHandler.SendRequestAndWaitForReceiptAsync(
                new NewItemFunction
                {
                    Id = 2,
                    Price = 100
                }).ConfigureAwait(false);

            var logs1__Result = await itemCreatedEvent.GetAllChangesAsync(eventFilter1__).ConfigureAwait(false);
            Assert.Single(logs1__Result);
            Assert.Equal(1, logs1__Result[0].Event.ItemId);
            Assert.Equal(100, logs1__Result[0].Event.Price);
            Assert.Equal(senderAddress.ToLower(), logs1__Result[0].Event.Result.ToLower());

            var logs__SenderAddress = await itemCreatedEvent.GetAllChangesAsync(eventFilter__SenderAddress).ConfigureAwait(false);
            Assert.Equal(2, logs__SenderAddress.Count);
            Assert.Contains(logs__SenderAddress, el => el.Event.ItemId == 1);
            Assert.Contains(logs__SenderAddress, el => el.Event.ItemId == 2);
        }

        public class TestAnonymousEventContractDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "608060405234801561001057600080fd5b5033600160006101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff1602179055506101b8806100616000396000f3fe608060405234801561001057600080fd5b5060043610610048576000357c01000000000000000000000000000000000000000000000000000000009004806329b856881461004d575b600080fd5b6100836004803603604081101561006357600080fd5b810190808035906020019092919080359060200190929190505050610085565b005b6000606060405190810160405280848152602001838152602001600160009054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681525090806001815401808255809150509060018203906000526020600020906003020160009091929091909150600082015181600001556020820151816001015560408201518160020160006101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff1602179055505050503373ffffffffffffffffffffffffffffffffffffffff16818360405160405180910390a3505056fea165627a7a72305820b762f7e2a9d04b5fbe6c2437c0471b855ad19c17c4a3fedb2cbe7d74a9d79cca0029";

            public TestAnonymousEventContractDeployment() : base(BYTECODE)
            {
            }

            public TestAnonymousEventContractDeployment(string byteCode) : base(byteCode)
            {
            }
        }

        [Event("ItemCreated", true)]
        public class ItemCreatedEventDTO : IEventDTO
        {
            [Parameter("uint256", "itemId", 1, true)]
            public BigInteger ItemId { get; set; }

            [Parameter("uint256", "price", 2, true)]
            public BigInteger Price { get; set; }

            [Parameter("address", "result", 3, true)]
            public string Result { get; set; }
        }

        [Function("newItem")]
        public class NewItemFunction : FunctionMessage
        {
            [Parameter("uint256", "id", 1)] public BigInteger Id { get; set; }

            [Parameter("uint256", "price", 2)] public BigInteger Price { get; set; }
        }

/* Contract 
contract TestAnonymousEventContract {
    struct Item {
        uint id;
        uint price;
        address manager;
    }

    Item[] items;
    address manager;

    constructor() public {
        manager = msg.sender;
    }
    
    event ItemCreated(uint indexed itemId, uint indexed price, address indexed result) anonymous;

    function newItem(uint id, uint price) public {
        items.push(Item(id, price, manager));
        emit ItemCreated(id, price, msg.sender);
    }
}
*/
    }
}