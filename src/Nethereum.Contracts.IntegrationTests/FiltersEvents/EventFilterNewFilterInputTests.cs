using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.IntegrationTests.CQS;
using Nethereum.JsonRpc.Client;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.FiltersEvents
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EventFilterNewFilterInputTests
    {
        public partial class EIP20Deployment : EIP20DeploymentBase
        {
            public EIP20Deployment() : base(BYTECODE) { }

            public EIP20Deployment(string byteCode) : base(byteCode) { }
        }

        public class EIP20DeploymentBase : ContractDeploymentMessage
        {

            public static string BYTECODE = "608060405234801561001057600080fd5b506040516107843803806107848339810160409081528151602080840151838501516060860151336000908152808552959095208490556002849055908501805193959094919391019161006991600391860190610096565b506004805460ff191660ff8416179055805161008c906005906020840190610096565b5050505050610131565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f106100d757805160ff1916838001178555610104565b82800160010185558215610104579182015b828111156101045782518255916020019190600101906100e9565b50610110929150610114565b5090565b61012e91905b80821115610110576000815560010161011a565b90565b610644806101406000396000f3006080604052600436106100ae5763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166306fdde0381146100b3578063095ea7b31461013d57806318160ddd1461017557806323b872dd1461019c57806327e235e3146101c6578063313ce567146101e75780635c6581651461021257806370a082311461023957806395d89b411461025a578063a9059cbb1461026f578063dd62ed3e14610293575b600080fd5b3480156100bf57600080fd5b506100c86102ba565b6040805160208082528351818301528351919283929083019185019080838360005b838110156101025781810151838201526020016100ea565b50505050905090810190601f16801561012f5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34801561014957600080fd5b50610161600160a060020a0360043516602435610348565b604080519115158252519081900360200190f35b34801561018157600080fd5b5061018a6103ae565b60408051918252519081900360200190f35b3480156101a857600080fd5b50610161600160a060020a03600435811690602435166044356103b4565b3480156101d257600080fd5b5061018a600160a060020a03600435166104b7565b3480156101f357600080fd5b506101fc6104c9565b6040805160ff9092168252519081900360200190f35b34801561021e57600080fd5b5061018a600160a060020a03600435811690602435166104d2565b34801561024557600080fd5b5061018a600160a060020a03600435166104ef565b34801561026657600080fd5b506100c861050a565b34801561027b57600080fd5b50610161600160a060020a0360043516602435610565565b34801561029f57600080fd5b5061018a600160a060020a03600435811690602435166105ed565b6003805460408051602060026001851615610100026000190190941693909304601f810184900484028201840190925281815292918301828280156103405780601f1061031557610100808354040283529160200191610340565b820191906000526020600020905b81548152906001019060200180831161032357829003601f168201915b505050505081565b336000818152600160209081526040808320600160a060020a038716808552908352818420869055815186815291519394909390927f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925928290030190a350600192915050565b60025481565b600160a060020a03831660008181526001602090815260408083203384528252808320549383529082905281205490919083118015906103f45750828110155b15156103ff57600080fd5b600160a060020a038085166000908152602081905260408082208054870190559187168152208054849003905560001981101561046157600160a060020a03851660009081526001602090815260408083203384529091529020805484900390555b83600160a060020a031685600160a060020a03167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef856040518082815260200191505060405180910390a3506001949350505050565b60006020819052908152604090205481565b60045460ff1681565b600160209081526000928352604080842090915290825290205481565b600160a060020a031660009081526020819052604090205490565b6005805460408051602060026001851615610100026000190190941693909304601f810184900484028201840190925281815292918301828280156103405780601f1061031557610100808354040283529160200191610340565b3360009081526020819052604081205482111561058157600080fd5b3360008181526020818152604080832080548790039055600160a060020a03871680845292819020805487019055805186815290519293927fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef929181900390910190a350600192915050565b600160a060020a039182166000908152600160209081526040808320939094168252919091522054905600a165627a7a7230582084c618322109054a21a57e27075384a6172ab854e4b2c2d35062a964a6bf593f0029";

            public EIP20DeploymentBase() : base(BYTECODE) { }

            public EIP20DeploymentBase(string byteCode) : base(byteCode) { }

            [Parameter("uint256", "_initialAmount", 1)]
            public BigInteger InitialAmount { get; set; }
            [Parameter("string", "_tokenName", 2)]
            public string TokenName { get; set; }
            [Parameter("uint8", "_decimalUnits", 3)]
            public byte DecimalUnits { get; set; }
            [Parameter("string", "_tokenSymbol", 4)]
            public string TokenSymbol { get; set; }

        }

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public EventFilterNewFilterInputTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void Test()
        {
            var senderAddress = AccountFactory.Address;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentMessage = new EIP20Deployment
            {
                InitialAmount = 10000,
                FromAddress = senderAddress,
                TokenName = "TST",
                TokenSymbol = "TST"
            };

            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<EIP20Deployment>();
            var transactionReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage)
                .ConfigureAwait(false);

            var contractAddress = transactionReceipt.ContractAddress;
            var newAddress = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";


            var transactionMessage = new TransferFunction
            {
                FromAddress = senderAddress,
                To = newAddress,
                TokenAmount = 1000,
               
            };

            var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();

            var transferReceipt =
                await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress, transactionMessage)
                    .ConfigureAwait(false);


            await EventAssertionsAsync(contractAddress, web3.Client, senderAddress, newAddress);
            await EventAssertionsAsync(null, web3.Client, senderAddress, newAddress);

        }

        public async Task EventAssertionsAsync(string contractAddresses,
            IClient client,
            string senderAddress,
            string newAddress)
        {
            Event<TransferEventDTO> eventForAnyContract = null; 

            if (contractAddresses == null)
            {
                eventForAnyContract = new Event<TransferEventDTO>(client);
            }
            else
            {
                eventForAnyContract = new Event<TransferEventDTO>(client, contractAddresses);
            }

            var filterInputForAllContracts = eventForAnyContract.CreateFilterInput();

            var event1 = await eventForAnyContract.GetAllChanges(filterInputForAllContracts);

            Assert.True(event1.Any());

            var filterInputForFromAddress = eventForAnyContract.CreateFilterInput(senderAddress);

            var event2 = await eventForAnyContract.GetAllChanges(filterInputForFromAddress);

            Assert.True(event2.Any());

            //Parity does not accept null values for filter
            if (_ethereumClientIntegrationFixture.Geth)
            {
                var filterInputForToAddress = eventForAnyContract.CreateFilterInput((string) null, newAddress);

                var event3 = await eventForAnyContract.GetAllChanges(filterInputForToAddress);

                Assert.True(event3.Any());
            }

            var filterInputForToAndFromAddress = eventForAnyContract.CreateFilterInput(senderAddress, newAddress);

            var event4 = await eventForAnyContract.GetAllChanges(filterInputForToAndFromAddress);
            Assert.True(event4.Any());


            var filterInputForFromAddressArray = eventForAnyContract.CreateFilterInput(new []{senderAddress});

            var event5 = await eventForAnyContract.GetAllChanges(filterInputForFromAddressArray);

            Assert.True(event5.Any());

            // Parity does not accept null values for filter
            if (_ethereumClientIntegrationFixture.Geth) {

                var filterInputForToAddressArray = eventForAnyContract.CreateFilterInput(new string[]{}, new[]{newAddress});

                var event6 = await eventForAnyContract.GetAllChanges(filterInputForToAddressArray);

                Assert.True(event6.Any());
            }

            var filterInputForToAndFromAddressArray = eventForAnyContract.CreateFilterInput(new []{senderAddress}, new []{newAddress});

            var event7 = await eventForAnyContract.GetAllChanges(filterInputForToAndFromAddressArray);
            Assert.True(event5.Any());
        }

    }
}