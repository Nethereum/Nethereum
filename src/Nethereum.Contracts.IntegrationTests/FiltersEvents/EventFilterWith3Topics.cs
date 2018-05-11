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

        [Event("Pushed")]
        public class PushedEventDTO
        {
            [Parameter("address", "first", 1, true)]
            public string First { get; set; }
            [Parameter("address", "second", 2, true)]
            public string Second { get; set; }
            [Parameter("bytes", "third", 3, false)]
            public byte[] Third { get; set; }
            [Parameter("bytes", "fourth", 4, false)]
            public byte[] Fourth { get; set; }
            [Parameter("bytes32", "fifth", 5, true)]
            public byte[] Fifth { get; set; }
        }


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


        [Event("Pushed2")]
        public class Pushed2EventDTO
        {
            [Parameter("address", "first", 1, true)]
            public string First { get; set; }
            [Parameter("address", "second", 2, true)]
            public string Second { get; set; }
            [Parameter("bytes32", "third", 3, true)]
            public byte[] Third { get; set; }
            [Parameter("bytes", "fourth", 4, false)]
            public byte[] Fourth { get; set; }
            [Parameter("bytes", "fifth", 5, false)]
            public byte[] Fifth { get; set; }
        }

        [Function("PushEvent")]
        public class PushEventFunction : ContractMessage
        {

        }

        public class TestEventDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                    "608060405234801561001057600080fd5b506103f0806100206000396000f3006080604052600436106100405763ffffffff7c0100000000000000000000000000000000000000000000000000000000600035041663b28dcc648114610045575b600080fd5b34801561005157600080fd5b5061005a61005c565b005b604080517f30313032303330340000000000000000000000000000000000000000000000008082526020820181905282519092606092849273ffffffffffffffffffffffffffffffffffffffff33169283927f5f7b4ef412a1639bb2acc0141a66f923c92070040b2c90d8a79c6d0966c03bfd929081900390910190a46040805160208082528183019092529080820161040080388339505081519192507f010000000000000000000000000000000000000000000000000000000000000091839150600090811061012a57fe5b9060200101907effffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff1916908160001a90535081600019163373ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167f53a4d306e452259eb29df0d47380abbde3f61e0f2ca4516e4d592aa40ced9afd8485604051808060200180602001838103835285818151815260200191508051906020019080838360005b838110156101f15781810151838201526020016101d9565b50505050905090810190601f16801561021e5780820380516001836020036101000a031916815260200191505b50838103825284518152845160209182019186019080838360005b83811015610251578181015183820152602001610239565b50505050905090810190601f16801561027e5780820380516001836020036101000a031916815260200191505b5094505050505060405180910390a481600019163373ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167fc4c107300ce6c6db32c2acd7c37b5c44c5fbdc3c065c6332b7234a66ed8686498485604051808060200180602001838103835285818151815260200191508051906020019080838360005b8381101561032457818101518382015260200161030c565b50505050905090810190601f1680156103515780820380516001836020036101000a031916815260200191505b50838103825284518152845160209182019186019080838360005b8381101561038457818101518382015260200161036c565b50505050905090810190601f1680156103b15780820380516001836020036101000a031916815260200191505b5094505050505060405180910390a450505600a165627a7a723058206506e94e4f63baa256b37d2ccb52196e7e2419b43b26fba54ec96427211fd3bb0029"
                ;

            public TestEventDeployment() : base(BYTECODE)
            {
            }

            public TestEventDeployment(string byteCode) : base(byteCode)
            {
            }
        }

        /*
         pragma solidity 0.4.23;
        contract TestEvent {

            //this is ok all 32 bytes
            event PushedResult(address indexed first, address indexed second, bytes32 third, bytes32 fourth, bytes32 indexed fifth);
    
            //This fails to create a filter
            event Pushed(
                address indexed first,
                address indexed second,
                bytes third,            
                bytes fourth,
                bytes32 indexed fifth 
            );


            //This is ok creating a filter
            event Pushed2(
                address indexed first,
                address indexed second,
                bytes32 indexed third,
                bytes fourth,            
                bytes fifth
            );

            function PushEvent() public
            {
                //0x3031303230333034000000000000000000000000000000000000000000000000
                bytes32 thing = bytes32("01020304");
                emit PushedResult(msg.sender, msg.sender, thing, thing, thing);
                bytes memory bytesArray = new bytes(32);
                bytesArray[0] = 0x01;
                emit Pushed(msg.sender, msg.sender, bytesArray, bytesArray, thing);    
                emit Pushed2(msg.sender, msg.sender, thing, bytesArray, bytesArray);
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

            //Event with all parameters fixed 32 bytes 2 addresses indexed and last indexed bytes 32
            var eventAllBytes32 = contractHandler.GetEvent<PushedResultEventDTO>();
            var filterAllBytes32 = await eventAllBytes32.CreateFilterAsync(addressFrom, addressFrom, bytes,
                new BlockParameter(receipt.BlockNumber), BlockParameter.CreateLatest());


            //Event with dynamic and last indexed bytes32
            var eventPushed = contractHandler.GetEvent<PushedEventDTO>();

            //ERROR creating filter
            //var filter2 = await eventPushed.CreateFilterAsync(addressFrom, addressFrom, bytes,
            //    new BlockParameter(receipt.BlockNumber), BlockParameter.CreateLatest());

            //Event with dynamic bytes all indexed values at the front
            var eventIndexedAtTheFront = contractHandler.GetEvent<Pushed2EventDTO>();
            var filterIndexedAtTheFront = await eventIndexedAtTheFront.CreateFilterAsync(addressFrom, addressFrom, bytes,
                new BlockParameter(receipt.BlockNumber), BlockParameter.CreateLatest());


            var pushReceipt = await contractHandler.SendRequestAndWaitForReceiptAsync(new PushEventFunction(){FromAddress =  addressFrom});

            // Getting changes from the event with all bytes32
            var filterChangesAllBytes32 = await eventAllBytes32.GetFilterChanges<PushedResultEventDTO>(filterAllBytes32);

            Assert.NotEmpty(filterChangesAllBytes32);

            Assert.Equal(addressFrom.ToLower(), filterChangesAllBytes32[0].Event.First.ToLower());

            //Decoding the event (that we cannot create a filter) from the transaction receipt
            var eventsPushed = eventPushed.DecodeAllEventsForEvent<PushedEventDTO>(pushReceipt.Logs);

            Assert.NotEmpty(eventsPushed);

            Assert.Equal(addressFrom.ToLower(), eventsPushed[0].Event.First.ToLower());


            // Getting changes from the event with indexed at the front
            var filterChangesIndexedAtTheFront = await eventIndexedAtTheFront.GetFilterChanges<PushedResultEventDTO>(filterIndexedAtTheFront);

            Assert.NotEmpty(filterChangesIndexedAtTheFront);

            Assert.Equal(addressFrom.ToLower(), filterChangesIndexedAtTheFront[0].Event.First.ToLower());
        }

    }
}
 