using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.CQS;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.FiltersEvents
{
    public class EventFilterWith3Topics
    {

        [Event("PushedResult")]
        public class PushedResultEventDTO
        {
            [Parameter("address", "first", 1, true)]
            public string First { get; set; }

            [Parameter("address", "second", 2, true)]
            public string Second { get; set; }

            [Parameter("bytes32", "third", 3, false)]
            public byte[] Third { get; set; }

            [Parameter("bytes32", "fourth", 4, false)]
            public byte[] Fourth { get; set; }

            [Parameter("bytes32", "fifth", 5, true)]
            public byte[] Fifth { get; set; }
        }

        [Function("PushEvent")]
        public class PushEventFunction : ContractMessage
        {

        }

        public class TestEventDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                    "6080604052348015600f57600080fd5b5060fd8061001e6000396000f300608060405260043610603e5763ffffffff7c0100000000000000000000000000000000000000000000000000000000600035041663b28dcc6481146043575b600080fd5b348015604e57600080fd5b5060556057565b005b604080517f30313032303330340000000000000000000000000000000000000000000000008082526020820181905282519092839273ffffffffffffffffffffffffffffffffffffffff33169283927f5f7b4ef412a1639bb2acc0141a66f923c92070040b2c90d8a79c6d0966c03bfd928290030190a4505600a165627a7a723058204c7b82d835fc9ca44a423c7260db5b1c3b001627fff2696849a231ea9378d7150029"
                ;

            public TestEventDeployment() : base(BYTECODE)
            {
            }

            public TestEventDeployment(string byteCode) : base(byteCode)
            {
            }
        }

        /*
         contract TestEvent {

            event PushedResult(address indexed first, address indexed second, bytes32 third, bytes32 fourth, bytes32 indexed fifth);

            function PushEvent() public
            {
                //0x3031303230333034000000000000000000000000000000000000000000000000
                bytes32 thing = bytes32("01020304");
                emit PushedResult(msg.sender, msg.sender, thing, thing, thing);    
            }
        }

        */

        [Fact]
        public async Task Test()
        {
            var web3 = Web3Factory.GetWeb3();
            var addressFrom = AccountFactory.Address;
            var receipt = await web3.Eth.GetContractDeploymentHandler<TestEventDeployment>()
                .SendRequestAndWaitForReceiptAsync(new TestEventDeployment(){FromAddress = addressFrom});


            var contractHandler = web3.Eth.GetContractHandler(receipt.ContractAddress);

            var bytes = "0x3031303230333034000000000000000000000000000000000000000000000000".HexToByteArray();


            var eventPushed = contractHandler.GetEvent<PushedResultEventDTO>();
            var filter = await eventPushed.CreateFilterAsync(addressFrom, addressFrom, bytes,
                new BlockParameter(receipt.BlockNumber), BlockParameter.CreateLatest());

            
            var pushReceipt = await contractHandler.SendRequestAndWaitForReceiptAsync(new PushEventFunction(){FromAddress =  addressFrom});

            var changes = await eventPushed.GetFilterChanges<PushedResultEventDTO>(filter);

            Assert.NotEmpty(changes);

            Assert.Equal(addressFrom.ToLower(), changes[0].Event.First.ToLower());

        }

    }
}
 