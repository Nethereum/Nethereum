using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.EncodingInputOutput
{
    public class FunctionOutputDTOTests
    {
        public class TestOutputService
        {
            public static string ABI =
                    @"[{'constant':false,'inputs':[],'name':'getData','outputs':[{'name':'birthTime','type':'uint64'},{'name':'userName','type':'string'},{'name':'starterId','type':'uint16'},{'name':'currLocation','type':'uint16'},{'name':'isBusy','type':'bool'},{'name':'owner','type':'address'}],'payable':false,'stateMutability':'nonpayable','type':'function'}]"
                ;

            public static string BYTE_CODE =
                    "0x6060604052341561000f57600080fd5b6101c88061001e6000396000f3006060604052600436106100405763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416633bc5de308114610045575b600080fd5b341561005057600080fd5b61005861011a565b60405167ffffffffffffffff8716815261ffff808616604083015284166060820152821515608082015273ffffffffffffffffffffffffffffffffffffffff821660a082015260c06020820181815290820187818151815260200191508051906020019080838360005b838110156100da5780820151838201526020016100c2565b50505050905090810190601f1680156101075780820380516001836020036101000a031916815260200191505b5097505050505050505060405180910390f35b600061012461018a565b6000806000806001955060408051908101604052600481527f6a75616e0000000000000000000000000000000000000000000000000000000060208201529596600195508594506000935073de0b295669a9fd93d5f28d9ec85e40f4cb697bae92509050565b602060405190810160405260008152905600a165627a7a72305820ba7625d1c6f0f2844d32ad76e28729e80979f69cbd32d0589995f24cb969a6850029"
                ; /*
            pragma solidity ^0.4.19;

            contract TestOutput {

                function getData() returns (uint64 birthTime, string userName, uint16 starterId, uint16 currLocation, bool isBusy, address owner ) {
                    birthTime = 1;
                    userName = "juan";
                    starterId = 1;
                    currLocation = 1;
                    isBusy = false;
                    owner = 0xde0b295669a9fd93d5f28d9ec85e40f4cb697bae;
                }
            }

            */

            private readonly Web3.Web3 web3;

            private readonly Contract contract;

            public TestOutputService(Web3.Web3 web3, string address)
            {
                this.web3 = web3;
                contract = web3.Eth.GetContract(ABI, address);
            }

            public Function GetFunctionGetData()
            {
                return contract.GetFunction("getData");
            }


            public Task<string> GetDataAsync(string addressFrom, HexBigInteger gas = null,
                HexBigInteger valueAmount = null)
            {
                var function = GetFunctionGetData();
                return function.SendTransactionAsync(addressFrom, gas, valueAmount);
            }

            public Task<GetDataDTO> GetDataAsyncCall()
            {
                var function = GetFunctionGetData();
                return function.CallDeserializingToObjectAsync<GetDataDTO>();
            }
        }

        [FunctionOutput]
        public class GetDataDTO
        {
            [Parameter("uint64", "birthTime", 1)]
            public ulong BirthTime { get; set; }

            [Parameter("string", "userName", 2)]
            public string UserName { get; set; }

            [Parameter("uint16", "starterId", 3)]
            public int StarterId { get; set; }

            [Parameter("uint16", "currLocation", 4)]
            public int CurrLocation { get; set; }

            [Parameter("bool", "isBusy", 5)]
            public bool IsBusy { get; set; }

            [Parameter("address", "owner", 6)]
            public string Owner { get; set; }
        }

        [Fact]
        public async void ShouldReturnFunctionOutputDTO()
        {
            var web3 = Web3Factory.GetWeb3();
            var senderAddress = AccountFactory.Address;

            var contractReceipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(TestOutputService.ABI,
                TestOutputService.BYTE_CODE, senderAddress, new HexBigInteger(900000));
            var service = new TestOutputService(web3, contractReceipt.ContractAddress);
            var message = await service.GetDataAsyncCall();
            Assert.Equal(1, (int) message.BirthTime);
            Assert.Equal(1, message.CurrLocation);
            Assert.Equal(1, message.StarterId);
            Assert.False(message.IsBusy);
            Assert.Equal("juan", message.UserName);
            Assert.Equal("0xde0b295669a9fd93d5f28d9ec85e40f4cb697bae", message.Owner);
        }
    }
}