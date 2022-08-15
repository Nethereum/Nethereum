using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.XUnitEthereumClients;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Contracts.IntegrationTests.EncodingInputOutput
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class IndexedEvents
    {
        /*
        contract EventTest {
           event Event(uint first, uint indexed second, uint third, uint indexed fourth);
           function sendEvent() {
               Event(1,2,3,4);
           }
        }
        */

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public IndexedEvents(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        public static string ABI =
            @"[{'constant':false,'inputs':[],'name':'sendEvent','outputs':[],'payable':false,'type':'function'},{'anonymous':false,'inputs':[{'indexed':false,'name':'first','type':'uint256'},{'indexed':true,'name':'second','type':'uint256'},{'indexed':false,'name':'third','type':'uint256'},{'indexed':true,'name':'fourth','type':'uint256'}],'name':'Event','type':'event'}]";

        public static string BYTE_CODE =
            "0x6060604052341561000f57600080fd5b5b60bd8061001e6000396000f300606060405263ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166332b7a7618114603c575b600080fd5b3415604657600080fd5b604c604e565b005b600460027f07392b89121f4d481601a3db92f2daf8c73cc086a942b4522c58abcbdeac4d016001600360405191825260208201526040908101905180910390a35b5600a165627a7a723058202af7567cbbe622cac1fcce7d7f4aa0be6c879974474c7f7580a9fea9d4dfa5850029";

        [Event("Event")]
        public class EventEventDTO : IEventDTO
        {
            [Parameter("uint256", "first", 1, false)]
            public BigInteger First { get; set; }

            [Parameter("uint256", "second", 2, true)]
            public BigInteger Second { get; set; }

            [Parameter("uint256", "third", 3, false)]
            public BigInteger Third { get; set; }

            [Parameter("uint256", "fourth", 4, true)]
            public BigInteger Fourth { get; set; }
        }

        [Fact]
        public async void ShouldBeParsedInAnyOrder()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;

            var receipt =
                await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(BYTE_CODE, senderAddress,
                    new HexBigInteger(900000), null).ConfigureAwait(false);
            var contract = web3.Eth.GetContract(ABI, receipt.ContractAddress);

            var function = contract.GetFunction("sendEvent");
            receipt = await function.SendTransactionAndWaitForReceiptAsync(senderAddress, new HexBigInteger(900000),
                null, null).ConfigureAwait(false);


            var eventLog = contract.GetEvent("Event");
            var events = eventLog.DecodeAllEventsForEvent<EventEventDTO>(receipt.Logs);

            Assert.Equal(1, events[0].Event.First);
            Assert.Equal(2, events[0].Event.Second);
            Assert.Equal(3, events[0].Event.Third);
            Assert.Equal(4, events[0].Event.Fourth);
        }

        [Fact]
        public async void ShouldBeParsedInAnyOrderUsingExtensions()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;

            var receipt =
                await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(BYTE_CODE, senderAddress,
                    new HexBigInteger(900000), null).ConfigureAwait(false);
            var contract = web3.Eth.GetContract(ABI, receipt.ContractAddress);

            var function = contract.GetFunction("sendEvent");
            receipt = await function.SendTransactionAndWaitForReceiptAsync(senderAddress, new HexBigInteger(900000),
                null, null).ConfigureAwait(false);

            var events = receipt.Logs.DecodeAllEvents<EventEventDTO>();

            Assert.Equal(1, events[0].Event.First);
            Assert.Equal(2, events[0].Event.Second);
            Assert.Equal(3, events[0].Event.Third);
            Assert.Equal(4, events[0].Event.Fourth);
        }
    }
}