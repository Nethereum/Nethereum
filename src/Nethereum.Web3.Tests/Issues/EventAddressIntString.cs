using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.TransactionReceipts;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Web3.Tests.Issues
{
    public class EventAddressIntString
    {
        public async void TestChinese()
        {
            var account = new Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");
            var web3 = new Web3(account, ClientFactory.GetClient());
            var pollingService = new TransactionReceiptPollingService(web3.TransactionManager);
            var contractAddress = await pollingService.DeployContractAndGetAddressAsync(() =>
              CoinService.DeployContractAsync(web3, account.Address, new HexBigInteger(4000000)));
            var coinService = new CoinService(web3, contractAddress);

            var input = new RaiseEventMetadataInput()
            {
                Creator = account.Address,
                Id = 101,
                Description = @"中国，China",
                Metadata = @"中国，China"
            };

            var txn = await coinService.RaiseEventMetadataAsync(account.Address, input, new HexBigInteger(4000000));
            var receipt = await pollingService.PollForReceiptAsync(txn);

            var metadataEvent = coinService.GetEventMetadataEvent();
            var metadata = await metadataEvent.GetAllChanges<MetadataEventEventDTO>(metadataEvent.CreateFilterInput());
            var result = metadata[0].Event;
            Assert.Equal(result.Creator.ToLower(), account.Address.ToLower());
            Assert.Equal(result.Id, 101);
            Assert.Equal(result.Metadata, @"中国，China");
            Assert.Equal(result.Description, @"中国，China");
        }

       [Fact]
       public async void Test()
       {
          var account = new Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");
          var web3 = new Web3(account, ClientFactory.GetClient());
          var pollingService = new TransactionReceiptPollingService(web3.TransactionManager);
            var contractAddress = await pollingService.DeployContractAndGetAddressAsync(() =>
              CoinService.DeployContractAsync(web3, account.Address, new HexBigInteger(4000000)));
          var coinService = new CoinService(web3, contractAddress);
          var txn = await coinService.MintAsync(account.Address, account.Address, 100, new HexBigInteger(4000000));
          var receipt = await pollingService.PollForReceiptAsync(txn);
          var eventSent = coinService.GetEventSent();
          var sent = await eventSent.GetAllChanges<SentEventDTO>(eventSent.CreateFilterInput());

          txn = await coinService.RaiseEventMetadataAsync(account.Address, account.Address, 100, "Description", "The metadata created here blah blah blah", new HexBigInteger(4000000));
          receipt = await pollingService.PollForReceiptAsync(txn);

          var metadataEvent = coinService.GetEventMetadataEvent();
          var metadata = await metadataEvent.GetAllChanges<MetadataEventEventDTO>(metadataEvent.CreateFilterInput(new BlockParameter(receipt.BlockNumber)));
          var result = metadata[0].Event;
          Assert.Equal(result.Creator.ToLower(), account.Address.ToLower());
          Assert.Equal(result.Id, 100);
          Assert.Equal(result.Metadata, "The metadata created here blah blah blah");
          Assert.Equal(result.Description, "Description");
        }


        /*
         pragma solidity ^0.4.14;

contract Coin {
    // The keyword "public" makes those variables
    // readable from outside.
    address public minter;
    mapping (address => uint) public balances;

    // Events allow light clients to react on
    // changes efficiently.
    event Sent(address from, uint amount, address to );

    // This is the constructor whose code is
    // run only when the contract is created.
    function Coin() {
        minter = msg.sender;
    }

    function mint(address receiver, uint amount) {
        if (msg.sender != minter) return;
        balances[receiver] += amount;
    }

    function send(address receiver, uint amount) {
        if (balances[msg.sender] < amount) return;
        balances[msg.sender] -= amount;
        balances[receiver] += amount;
        Sent(msg.sender, amount, receiver);
    }

    event MetadataEvent(address creator, int id, string description, string metadata);

    function RaiseEventMetadata(address creator, int id, string description, string metadata ){
        MetadataEvent(creator, id, description, metadata);
    }
}

        */

        public class CoinService
        {
            private readonly Web3 web3;

            public static string ABI = @"[{'constant':true,'inputs':[],'name':'minter','outputs':[{'name':'','type':'address'}],'payable':false,'type':'function'},{'constant':true,'inputs':[{'name':'','type':'address'}],'name':'balances','outputs':[{'name':'','type':'uint256'}],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'receiver','type':'address'},{'name':'amount','type':'uint256'}],'name':'mint','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'creator','type':'address'},{'name':'id','type':'int256'},{'name':'description','type':'string'},{'name':'metadata','type':'string'}],'name':'RaiseEventMetadata','outputs':[],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'receiver','type':'address'},{'name':'amount','type':'uint256'}],'name':'send','outputs':[],'payable':false,'type':'function'},{'inputs':[],'payable':false,'type':'constructor'},{'anonymous':false,'inputs':[{'indexed':false,'name':'from','type':'address'},{'indexed':false,'name':'amount','type':'uint256'},{'indexed':false,'name':'to','type':'address'}],'name':'Sent','type':'event'},{'anonymous':false,'inputs':[{'indexed':false,'name':'creator','type':'address'},{'indexed':false,'name':'id','type':'int256'},{'indexed':false,'name':'description','type':'string'},{'indexed':false,'name':'metadata','type':'string'}],'name':'MetadataEvent','type':'event'}]";

            public static string BYTE_CODE = "0x6060604052341561000f57600080fd5b5b60008054600160a060020a03191633600160a060020a03161790555b5b61041a8061003c6000396000f300606060405263ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166307546172811461006957806327e235e31461009857806340c10f19146100c9578063c0cafcbb146100ed578063d0679d3414610196575b600080fd5b341561007457600080fd5b61007c6101ba565b604051600160a060020a03909116815260200160405180910390f35b34156100a357600080fd5b6100b7600160a060020a03600435166101c9565b60405190815260200160405180910390f35b34156100d457600080fd5b6100eb600160a060020a03600435166024356101db565b005b34156100f857600080fd5b6100eb60048035600160a060020a03169060248035919060649060443590810190830135806020601f8201819004810201604051908101604052818152929190602084018383808284378201915050505050509190803590602001908201803590602001908080601f01602080910402602001604051908101604052818152929190602084018383808284375094965061021995505050505050565b005b34156101a157600080fd5b6100eb600160a060020a036004351660243561033f565b005b600054600160a060020a031681565b60016020526000908152604090205481565b60005433600160a060020a039081169116146101f657610215565b600160a060020a03821660009081526001602052604090208054820190555b5050565b7f088e204f1e4b42de930e87cb772757d4fe2dac2efa50bb4b9d6b6c8669c31d4d84848484604051600160a060020a038516815260208101849052608060408201818152906060830190830185818151815260200191508051906020019080838360005b838110156102965780820151818401525b60200161027d565b50505050905090810190601f1680156102c35780820380516001836020036101000a031916815260200191505b50838103825284818151815260200191508051906020019080838360005b838110156102fa5780820151818401525b6020016102e1565b50505050905090810190601f1680156103275780820380516001836020036101000a031916815260200191505b50965050505050505060405180910390a15b50505050565b600160a060020a0333166000908152600160205260409020548190101561036557610215565b600160a060020a03338181166000908152600160205260408082208054869003905592851681528290208054840190557f197260fb0c64c295dfe7074f7a13f7d1dee6f994b2be2f1c70d2332a64526e38918390859051600160a060020a03938416815260208101929092529091166040808301919091526060909101905180910390a15b50505600a165627a7a72305820fb59e26777a80c533713392891786e6db6d3e60117c2cd734a1e45a1f26c3ed90029";

            public static Task<string> DeployContractAsync(Web3 web3, string addressFrom, HexBigInteger gas = null, HexBigInteger valueAmount = null)
            {
                return web3.Eth.DeployContract.SendRequestAsync(ABI, BYTE_CODE, addressFrom, gas, valueAmount);
            }

            private Nethereum.Contracts.Contract contract;

            public CoinService(Web3 web3, string address)
            {
                this.web3 = web3;
                this.contract = web3.Eth.GetContract(ABI, address);
            }

            public Function GetFunctionMinter()
            {
                return contract.GetFunction("minter");
            }
            public Function GetFunctionBalances()
            {
                return contract.GetFunction("balances");
            }
            public Function GetFunctionMint()
            {
                return contract.GetFunction("mint");
            }
            public Function GetFunctionRaiseEventMetadata()
            {
                return contract.GetFunction("RaiseEventMetadata");
            }
            public Function GetFunctionSend()
            {
                return contract.GetFunction("send");
            }

            public Event GetEventSent()
            {
                return contract.GetEvent("Sent");
            }
            public Event GetEventMetadataEvent()
            {
                return contract.GetEvent("MetadataEvent");
            }

            public Task<string> MinterAsyncCall()
            {
                var function = GetFunctionMinter();
                return function.CallAsync<string>();
            }
            public Task<BigInteger> BalancesAsyncCall(string a)
            {
                var function = GetFunctionBalances();
                return function.CallAsync<BigInteger>(a);
            }

            public Task<string> MintAsync(string addressFrom, string receiver, BigInteger amount, HexBigInteger gas = null, HexBigInteger valueAmount = null)
            {
                var function = GetFunctionMint();
                return function.SendTransactionAsync(addressFrom, gas, valueAmount, receiver, amount);
            }
            public Task<string> RaiseEventMetadataAsync(string addressFrom, string creator, BigInteger id, string description, string metadata, HexBigInteger gas = null, HexBigInteger valueAmount = null)
            {
                var function = GetFunctionRaiseEventMetadata();
                return function.SendTransactionAsync(addressFrom, gas, valueAmount, creator, id, description, metadata);
            }

            public Task<string> RaiseEventMetadataAsync(string addressFrom, RaiseEventMetadataInput input, HexBigInteger gas = null, HexBigInteger valueAmount = null)
            {
                var function = contract.GetFunction<RaiseEventMetadataInput>();
                return function.SendTransactionAsync(input, addressFrom,  gas, valueAmount);
            }

            public Task<string> SendAsync(string addressFrom, string receiver, BigInteger amount, HexBigInteger gas = null, HexBigInteger valueAmount = null)
            {
                var function = GetFunctionSend();
                return function.SendTransactionAsync(addressFrom, gas, valueAmount, receiver, amount);
            }



        }

        [Function("RaiseEventMetadata")]
        public class RaiseEventMetadataInput
        {
            [Parameter("address", "creator", 1, false)]
            public string Creator { get; set; }

            [Parameter("int256", "id", 2, false)]
            public int Id { get; set; }

            [Parameter("string", "description", 3, false)]
            public string Description { get; set; }

            [Parameter("string", "metadata", 4, false)]
            public string Metadata { get; set; }
        }


        public class SentEventDTO
        {
            [Parameter("address", "from", 1, false)]
            public string From { get; set; }

            [Parameter("uint256", "amount", 2, false)]
            public BigInteger Amount { get; set; }

            [Parameter("address", "to", 3, false)]
            public string To { get; set; }

        }

        public class MetadataEventEventDTO
        {
            [Parameter("address", "creator", 1, false)]
            public string Creator { get; set; }

            [Parameter("int256", "id", 2, false)]
            public int Id { get; set; }

            [Parameter("string", "description", 3, false)]
            public string Description { get; set; }

            [Parameter("string", "metadata", 4, false)]
            public string Metadata { get; set; }

        }

    }
}