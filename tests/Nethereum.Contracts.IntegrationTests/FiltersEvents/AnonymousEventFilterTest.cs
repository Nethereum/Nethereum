using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.XUnitEthereumClients;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

// ReSharper disable ConsiderUsingConfigureAwait

namespace Nethereum.Contracts.IntegrationTests.FiltersEvents
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class AnonymousEventFilterTest
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public AnonymousEventFilterTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
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

            var eventFilter = contractHandler.GetEvent<ItemCreatedEventDTO>();
            var filterId = await eventFilter.CreateFilterAsync(1).ConfigureAwait(false);

            var transactionReceiptSend = await contractHandler.SendRequestAndWaitForReceiptAsync(
                new NewItemFunction()
                {
                    FromAddress = senderAddress,
                    Id = 1,
                    Price = 100
                }).ConfigureAwait(false);

            var result = await eventFilter.GetFilterChangesAsync(filterId).ConfigureAwait(false);

            Assert.Single(result);
        }

        public class TestAnonymousEventContractDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "608060405234801561001057600080fd5b5033600160006101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff1602179055506101d7806100616000396000f3fe608060405234801561001057600080fd5b5060043610610048576000357c01000000000000000000000000000000000000000000000000000000009004806329b856881461004d575b600080fd5b6100836004803603604081101561006357600080fd5b810190808035906020019092919080359060200190929190505050610085565b005b6000606060405190810160405280848152602001838152602001600160009054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681525090806001815401808255809150509060018203906000526020600020906003020160009091929091909150600082015181600001556020820151816001015560408201518160020160006101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff1602179055505050508133604051808273ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390a1505056fea165627a7a72305820c62dadf2e61a3d98a2b9997af83269a815a4166e6610807642db0b3771896a220029";

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

            [Parameter("address", "result", 2, false)]
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
    
    event ItemCreated(uint indexed itemId, address result) anonymous;

    function newItem(uint id, uint price) public {
        items.push(Item(id, price, manager));
        emit ItemCreated(id, msg.sender);
    }
} 
*/
    }
}